using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MikroSharp.Tests;

public class DictionarySerializationTests
{
    [Fact]
    public void JsonSerializer_Should_Preserve_Dictionary_Keys_As_Is()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
        };

        var body = new Dictionary<string, object?>
        {
            ["shared-users"] = 2,
            ["group"] = "default",
            ["starts-when"] = "assigned",
            ["transfer-limit"] = "1024B"
        };

        var json = JsonSerializer.Serialize(body, options);

        json.Should().Contain("\"shared-users\":2");
        json.Should().Contain("\"group\":\"default\"");
        json.Should().Contain("\"starts-when\":\"assigned\"");
        json.Should().Contain("\"transfer-limit\":\"1024B\"");
        json.Should().NotContain("g-roup");
    }
}
