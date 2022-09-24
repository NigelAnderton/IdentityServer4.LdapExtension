using IdentityServer.LdapExtension.Exceptions;
using IdentityServer.LdapExtension.UserModel;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.LdapExtension
{
    /// <summary>
    /// This is an implementation of the service that is used to contact Ldap.
    /// </summary>
    public class LdapService<TUser> : ILdapService<TUser>
        where TUser : IAppUser, new()
    {
        private readonly ILogger<LdapService<TUser>> _logger;
        private readonly LdapConfig[] _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="LdapService{TUser}"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="logger">The logger.</param>
        public LdapService(ExtensionConfig config, ILogger<LdapService<TUser>> logger)
        {
            _logger = logger;
            _config = config.Connections.ToArray();
        }

        /// <summary>
        /// Logins using the specified credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>
        /// Returns the logged in user.
        /// </returns>
        /// <exception cref="LoginFailedException">Login failed.</exception>
        public TUser Login(string username, string password)
        {
            return Login(username, password, null);
        }

        /// <summary>
        /// Logins using the specified credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="domain">The domain friendly name.</param>
        /// <returns>
        /// Returns the logged in user.
        /// </returns>
        /// <exception cref="LoginFailedException">Login failed.</exception>
        public TUser Login(string username, string password, string domain)
        {
            var searchResult = SearchUser(username, domain);

            var searchTask = Task.Run(async () => await searchResult.Result.Results.AnyAsync());
            if (searchTask.Result)
            {
                try
                {
                    var task = Task.Run(async () => await searchResult.Result.Results.FirstOrDefaultAsync());
                    var user = task.Result;
                    if (user != null)
                    {
                        searchResult.Result.LdapConnection.BindAsync(user.Dn, password);
                        if (searchResult.Result.LdapConnection.Bound)
                        {
                            //could change to ldap or change to configurable option
                            var provider = !string.IsNullOrEmpty(domain) ? domain : "local";
                            var appUser = new TUser();
                            appUser.SetBaseDetails(user, provider, searchResult.Result.config.ExtraAttributes); // Should we change to LDAP.
                            searchResult.Result.LdapConnection.Disconnect();

                            return appUser;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogTrace("{EMessage}", e.Message);
                    _logger.LogTrace("{EStackTrace}", e.StackTrace);
                    throw new LoginFailedException("Login failed.", e);
                }
            }

            searchResult.Result.LdapConnection.Disconnect();

            return default(TUser);
        }

        /// <summary>
        /// Finds user by username.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>
        /// Returns the user when it exists.
        /// </returns>
        public TUser FindUser(string username)
        {
            return FindUser(username, null);
        }

        /// <summary>
        /// Finds user by username.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="domain">The domain friendly name.</param>
        /// <returns>
        /// Returns the user when it exists.
        /// </returns>
        public TUser FindUser(string username, string domain)
        {
            var searchResult = SearchUser(username, domain);

            try
            {
                var task = Task.Run(async () => await searchResult.Result.Results.FirstOrDefaultAsync());
                var user = task.Result;
                if (user != null)
                {
                    //could change to ldap or change to configurable option
                    var provider = !string.IsNullOrEmpty(domain) ? domain : "local";
                    var appUser = new TUser();
                    appUser.SetBaseDetails(user, provider, searchResult.Result.config.ExtraAttributes);

                    searchResult.Result.LdapConnection.Disconnect();

                    return appUser;
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(default, e, "{EMessage}", e.Message);
                // Swallow the exception since we don't expect an error from this method.
            }

            //searchResult.LdapConnection.Disconnect();

            return default(TUser);
        }

        private async Task<(ILdapSearchResults Results, LdapConnection LdapConnection, LdapConfig config)> SearchUser(string username, string domain)
        {
            var allSearchable = _config.Where(f => f.IsConcerned(username)).ToList();
            if (!string.IsNullOrEmpty(domain))
            {
                allSearchable = allSearchable.Where(e => e.FriendlyName.Equals(domain)).ToList();
            }

            if (allSearchable == null || !allSearchable.Any())
            {
                throw new LoginFailedException(
                    "Login failed.",
                    new NoLdapSearchableException("No searchable LDAP"));
            }

            // Could become async
            foreach (var matchConfig in allSearchable)
            {
                using var ldapConnection = new LdapConnection { SecureSocketLayer = matchConfig.Ssl };
                await ldapConnection.ConnectAsync(matchConfig.Url, matchConfig.FinalLdapConnectionPort);
                await ldapConnection.BindAsync(matchConfig.BindDn, matchConfig.BindCredentials);
                
                var attributes = new TUser().LdapAttributes;
                var extraFieldList = new List<string>();

                if (matchConfig.ExtraAttributes != null)
                {
                    extraFieldList.AddRange(matchConfig.ExtraAttributes);
                }


                attributes = attributes.Concat(extraFieldList).ToArray();

                var searchFilter = string.Format(matchConfig.SearchFilter, username);
                var result = await ldapConnection.SearchAsync(
                    matchConfig.SearchBase,
                    LdapConnection.ScopeSub,
                    searchFilter,
                    attributes,
                    false
                );

                if (await result.AnyAsync()) // Count is async (not waiting). The hasMore() always works.
                {
                    return (Results: result as LdapSearchResults, LdapConnection: ldapConnection, matchConfig);
                }
            }

            throw new LoginFailedException(
                    "Login failed.",
                    new UserNotFoundException("User not found in any LDAP."));
        }
    }
}
