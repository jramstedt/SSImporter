using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.CodeDom;
using System.CodeDom.Compiler;

using SystemShock.Object;

namespace SSImporter.Resource {
    public class SystemShockObjectGenerator : MonoBehaviour {
        [MenuItem("Assets/System Shock/3. Generate Object Instance Classes", false, 1003)]
        public static void Init() {
            try {
                AssetDatabase.StartAssetEditing();
                EditorApplication.LockReloadAssemblies();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                if (!Directory.Exists(Application.dataPath + @"/SystemShock/InstanceObjects"))
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"InstanceObjects");


                foreach (string className in Enum.GetNames(typeof(ObjectClass)))
                    GenerateInstanceClass(className);
            } finally {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/System Shock/3. Generate Object Instance Classes", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        public static void GenerateInstanceClass(string Name) {
            string fullNamespace = @"SystemShock.InstanceObjects";
            string filePath = Application.dataPath + @"/SystemShock/InstanceObjects/" + Name + @".cs";

            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeNamespace globalNs = new CodeNamespace();
            globalNs.Imports.Add(new CodeNamespaceImport(@"UnityEngine"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"System"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"SystemShock.DataObjects"));

            compileUnit.Namespaces.Add(globalNs);

            CodeNamespace ns = new CodeNamespace(fullNamespace);

            Type instanceType = Type.GetType(typeof(SystemShock.Object.ObjectInstance).FullName + @"+" + Name + @", Assembly-CSharp");

            #region Instance object
            {
                CodeTypeDeclaration dataObjectClass = new CodeTypeDeclaration(Name);
                dataObjectClass.IsClass = true;
                dataObjectClass.IsPartial = true;
                dataObjectClass.TypeAttributes = TypeAttributes.Public;
                dataObjectClass.BaseTypes.Add(typeof(SystemShockObject<>).MakeGenericType(instanceType));
                dataObjectClass.CustomAttributes.Add(new CodeAttributeDeclaration(@"Serializable"));

                ns.Types.Add(dataObjectClass);
            }
            #endregion

            compileUnit.Namespaces.Add(ns);

            CodeDomProvider codeProvider = CodeDomProvider.CreateProvider(@"CSharp");
            CodeGeneratorOptions codeGeneratorOptions = new CodeGeneratorOptions();
            using (StreamWriter sourceWriter = new StreamWriter(filePath))
                codeProvider.GenerateCodeFromCompileUnit(compileUnit, sourceWriter, codeGeneratorOptions);
        }
    }
}