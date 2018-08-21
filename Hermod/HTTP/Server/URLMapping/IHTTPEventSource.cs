using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{
    public interface IHTTPEventSource
    {
        HTTPEventSource_Id EventIdentification { get; }
        Func<string, DateTime, string> LogfileName { get; }
        ulong MaxNumberOfCachedEvents { get; }
        TimeSpan RetryIntervall { get; set; }

        IEnumerable<HTTPEvent> GetAllEventsGreater(ulong? LastEventId = 0);
        IEnumerable<HTTPEvent> GetAllEventsSince(DateTime Timestamp);
        IEnumerator<HTTPEvent> GetEnumerator();
        Task SubmitEvent(JObject JSONObject);
        Task SubmitEvent(params string[] Data);
        Task SubmitSubEvent(string SubEvent, JObject JSONObject);
        Task SubmitSubEvent(string SubEvent, params string[] Data);
        Task SubmitSubEventWithTimestamp(string SubEvent, JObject JSONObject);
        Task SubmitSubEventWithTimestamp(string SubEvent, params string[] Data);
        Task SubmitTimestampedEvent(DateTime Timestamp, JObject JSONObject);
        Task SubmitTimestampedEvent(DateTime Timestamp, params string[] Data);
        Task SubmitTimestampedSubEvent(string SubEvent, DateTime Timestamp, JObject JSONObject);
        Task SubmitTimestampedSubEvent(string SubEvent, DateTime Timestamp, params string[] Data);
        string ToString();
    }
}