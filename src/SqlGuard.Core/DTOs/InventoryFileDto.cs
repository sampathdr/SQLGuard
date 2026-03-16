using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SqlGuard.Core.DTOs
{
    internal sealed class InventoryFileDto
    {
        [JsonPropertyName("default_packs")]
        public List<string>? DefaultPacks { get; set; }

        [JsonPropertyName("servers")]
        public List<InventoryServerDto> Servers { get; set; } = [];
    }
}
