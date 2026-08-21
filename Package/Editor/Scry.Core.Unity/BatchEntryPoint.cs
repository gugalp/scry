using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scry.Core.Unity
{
    public static class BatchEntryPoint
    {
        public const int ExitCodeSuccess = 0;
        public const int ExitCodeValidationFailure = 1;
        public const int ExitCodeToolError = 2;

        public static void ValidateCollection()
        {
            var exitCode = Run(Environment.GetCommandLineArgs());
            EditorApplication.Exit(exitCode);
        }

        public static int Run(string[] args)
        {
            try
            {
                var typeName = GetArgValue(args, "-scryType");
                if (typeName == null)
                {
                    Debug.LogError("Scry: missing required -scryType <AssemblyQualifiedName> argument.");
                    return ExitCodeToolError;
                }

                var type = Type.GetType(typeName);
                if (type == null)
                {
                    Debug.LogError($"Scry: could not resolve type '{typeName}'.");
                    return ExitCodeToolError;
                }

                var repository = new ScriptableObjectRepository();
                var collection = repository.Scan(type);

                foreach (var field in collection.Schema.Fields.Where(f => !f.IsSupported))
                    Debug.LogWarning($"Scry: field '{field.Name}' on '{typeName}' has an unmapped type and was skipped.");

                Debug.Log($"Scry: scanned {collection.Records.Count} record(s) of type '{typeName}'.");
                return ExitCodeSuccess;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Scry: tool error - {ex}");
                return ExitCodeToolError;
            }
        }

        public static string GetArgValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length)
                return null;

            return args[index + 1];
        }
    }
}
