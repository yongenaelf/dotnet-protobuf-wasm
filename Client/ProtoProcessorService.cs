extern alias Pbnetref;

using System;
using System.IO;
using System.Threading.Tasks;

namespace Client;

public class ProtoProcessorService
{
    public Task<IEnumerable<NameText>?> ProcessProtoFileAsync(string protoContent)
    {
        try
        {
            var set = new Pbnetref::Google.Protobuf.Reflection.FileDescriptorSet();
            using (var reader = new StringReader(protoContent))
            {
                set.Add("input.proto", true, reader);
            }
            set.Process();

            var codeGenerator = Pbnetref::ProtoBuf.Reflection.CSharpCodeGenerator.Default;

            IEnumerable<Pbnetref::ProtoBuf.Reflection.CodeFile>? codeFiles = codeGenerator.Generate(set);

            // convert to IEnumerable<NameText>
            return Task.FromResult(codeFiles?.Select(x => new NameText { Name = x.Name, Text = x.Text }));
        }
        catch (Exception ex)
        {
            // Handle exceptions
            throw new Exception("An error occurred while generating code", ex);
        }
    }
}

public class NameText
{
    public string Name { get; set; }
    public string Text { get; set; }
}