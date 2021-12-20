// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks
{
    public class TagHelperSerializationBenchmark : TagHelperBenchmarkBase
    {
        [Benchmark(Description = "(MessagePack) TagHelper Roundtrip Serialization")]
        public async Task MessagePack_TagHelper_Serialization_RoundTripAsync()
        {
            // Serialize back to json.
            MemoryStream originalStream;
            using (originalStream = new MemoryStream())
            {
                await MessagePackSerializer.SerializeAsync(originalStream, DefaultTagHelpers).ConfigureAwait(false);
            }

            IReadOnlyList<TagHelperDescriptor> reDeserializedTagHelpers;
            var stream = new MemoryStream(originalStream.GetBuffer());
            using (stream)
            {
                reDeserializedTagHelpers = await MessagePackSerializer.DeserializeAsync<IReadOnlyList<TagHelperDescriptor>>(stream).ConfigureAwait(false);
            }
        }

        [Benchmark(Description = "(MessagePack) TagHelper Serialization")]
        public async Task MessagePack_TagHelper_SerializationAsync()
        {
            using var stream = new MemoryStream();
            await MessagePackSerializer.SerializeAsync(stream, DefaultTagHelpers).ConfigureAwait(false);
        }

        [Benchmark(Description = "(MessagePack) TagHelper Deserialization")]
        public async Task MessagePack_TagHelper_DeserializationAsync()
        {
            // Deserialize from json file.
            using var stream = new MemoryStream(TagHelperMessagePackBuffer);
            var tagHelpers = await MessagePackSerializer.DeserializeAsync<IReadOnlyList<TagHelperDescriptor>>(stream).ConfigureAwait(false);
        }

        [Benchmark(Description = "(Newtonsoft) TagHelper Roundtrip Serialization")]
        public void TagHelper_Serialization_RoundTrip()
        {
            // Serialize back to json.
            MemoryStream originalStream;
            using (originalStream = new MemoryStream())
            using (var writer = new StreamWriter(originalStream, Encoding.UTF8, bufferSize: 4096))
            {
                DefaultSerializer.Serialize(writer, DefaultTagHelpers);
            }

            IReadOnlyList<TagHelperDescriptor> reDeserializedTagHelpers;
            var stream = new MemoryStream(originalStream.GetBuffer());
            using (stream)
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                reDeserializedTagHelpers = DefaultSerializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader);
            }
        }

        [Benchmark(Description = "(Newtonsoft) TagHelper Serialization")]
        public void TagHelper_Serialization()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096);
            DefaultSerializer.Serialize(writer, DefaultTagHelpers);
        }

        [Benchmark(Description = "(Newtonsoft) TagHelper Deserialization")]
        public void TagHelper_Deserialization()
        {
            // Deserialize from json file.
            IReadOnlyList<TagHelperDescriptor> tagHelpers;
            using var stream = new MemoryStream(TagHelperBuffer);
            using var reader = new JsonTextReader(new StreamReader(stream));
            tagHelpers = DefaultSerializer.Deserialize<IReadOnlyList<TagHelperDescriptor>>(reader);
        }

        [Benchmark(Description = "TagHelpers GetHashCode")]
        public void Benchmark_TagHelpersGetHashCode()
        {
            for (var i = 0; i < DefaultTagHelpers.Count; i++)
            {
                _ = DefaultTagHelpers[i].GetHashCode();
            }
        }
    }
}
