﻿using Newtonsoft.Json;

namespace MicrosoftStore.Responses
{
    public class Payload
    {
        [JsonProperty(PropertyName = "$type")]
        public string TypeName { get; set; }
    }

    public class ProductListPayload : Payload
    {
        public string ListType { get; set; }
        public string ListId { get; set; }
        public string Anid { get; set; }
        public string Title { get; set; }
        public bool HasThirdPartyIAPs { get; set; }
        public string AlgoName { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
    }
}
