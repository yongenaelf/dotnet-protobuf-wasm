using ProtoBuf.Reflection;
using Google.Protobuf.Reflection;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Client;

public class ProtoProcessorService
{
    public Task<IEnumerable<CodeFile>?> ProcessProtoFileAsync(string protoContent)
    {
        try
        {
            var set = new FileDescriptorSet();
            using (var reader = new StringReader(protoContent))
            {
                set.Add("input.proto", true, reader);
            }
            set.Process();

            var codeGenerator = CSharpCodeGenerator.Default;

            return Task.FromResult(codeGenerator.Generate(set));
        }
        catch (Exception ex)
        {
            // Handle exceptions
            throw new Exception("An error occurred while generating code", ex);
        }
    }
}