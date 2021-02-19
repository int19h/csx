﻿using System;
using System.CodeDom.Compiler;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp;

namespace Int19h.Bannerlord.CSharp.Scripting {
    public class Scripts : DynamicObject {
        internal static ScriptOptions GetScriptOptions() {
            var rsp = RspFile.Generated.Parse();
            var refs = rsp.ResolveMetadataReferences(ScriptMetadataResolver.Default);
            return ScriptOptions.Default
                .WithEmitDebugInformation(true)
                .AddReferences(refs)
                .AddImports(rsp.CompilationOptions.Usings);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[] args, out object? result) {
            result = null;

            var scriptName = binder.Name + "()";
            var fileName = ScriptFiles.GetFileName(scriptName);
            if (fileName == null) {
                throw new FileNotFoundException($"Function script not found: {scriptName}");
            }

            var provider = new CSharpCodeProvider();
            var codegenOptions = new CodeGeneratorOptions();
            var code = new StringWriter();
            code.WriteLine($"#load \"{fileName}\"");

            int posCount = binder.CallInfo.ArgumentCount - binder.CallInfo.ArgumentNames.Count;
            var argNames = Enumerable.Repeat((string?)null, posCount).Concat(binder.CallInfo.ArgumentNames).ToArray();
            code.WriteLine($"#line 1 \"{fileName}\"");
            code.Write($"return (Action<dynamic[]>)(args => {binder.Name}(");
            for (int i = 0; i < args.Length; ++i) {
                if (i != 0) {
                    code.Write(", ");
                }

                var argName = argNames[i];
                if (argName != null) {
                    code.Write($"{argName}: ");
                }
                code.Write($"args[{i}]");
            }
            code.WriteLine("));");

            var invoker = (Action<object?[]>)CSharpScript.EvaluateAsync(code.ToString(), GetScriptOptions()).GetAwaiter().GetResult();
            var oldScriptPath = ScriptGlobals.ScriptPath;
            ScriptGlobals.ScriptPath = fileName;
            args = args.Select(arg => ScriptArgument.Wrap(arg)).ToArray();
            try {
                invoker(args);
                return true;
            } catch (TargetInvocationException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            } finally {
                ScriptGlobals.ScriptPath = oldScriptPath;
            }
        }
    }
}
