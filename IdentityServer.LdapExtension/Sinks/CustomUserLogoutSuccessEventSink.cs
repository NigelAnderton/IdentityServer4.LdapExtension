using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;

namespace IdentityServer.LdapExtension
{
    /// <summary>
    /// Should be an event when the user sign out. This way we could push details to the
    /// redis cache or other source.
    /// </summary>
    public class CustomUserLogoutSuccessEventSink : IEventSink
    {
        private readonly ILogger<CustomUserLogoutSuccessEventSink> _log;

        public CustomUserLogoutSuccessEventSink(ILogger<CustomUserLogoutSuccessEventSink> logger)
        {
            _log = logger;
        }

        public Task PersistAsync(Event evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            
            var json = JsonSerializer.Serialize(evt);
            _log.LogInformation("{Json}", json);

            return Task.CompletedTask;
            // Not working at the moment. In the doc it says to register the DI, but it still not work.
#pragma warning disable CS0162
            // ReSharper disable once HeuristicUnreachableCode
            _log.LogDebug("{EType}", evt.EventType.ToString());
            _log.LogDebug("{EId}", evt.Id.ToString());
            _log.LogDebug("{EName}", evt.Name);
            _log.LogDebug("{EMessage}", evt.Message);

            return Task.CompletedTask;
#pragma warning restore CS0162
        }
    }
}
