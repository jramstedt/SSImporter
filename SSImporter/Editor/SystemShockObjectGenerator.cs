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
        [MenuItem("Assets/System Shock/4. Generate Object Instance Classes", false, 1004)]
        public static void Init() {
            try {
                AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                if (!Directory.Exists(Application.dataPath + @"/SystemShock/InstanceObjects"))
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"InstanceObjects");


                foreach (string className in Enum.GetNames(typeof(ObjectClass)))
                    GenerateInstanceClass(className);
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/System Shock/4. Generate Object Instance Classes", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        public static void GenerateInstanceClass(string Name) {
            string fullNamespace = @"SystemShock.InstanceObjects";
            //string fullName = fullNamespace + "." + Name;
            string filePath = Application.dataPath + @"/SystemShock/InstanceObjects/" + Name + @".cs";

            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeNamespace globalNs = new CodeNamespace();
            globalNs.Imports.Add(new CodeNamespaceImport(@"UnityEngine"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"System"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"SystemShock.DataObjects"));

            compileUnit.Namespaces.Add(globalNs);

            CodeNamespace ns = new CodeNamespace(fullNamespace);

            Type instanceType = Type.GetType(typeof(SystemShock.Object.ObjectInstance).FullName + @"+" + Name + @", Assembly-CSharp");

            CodeTypeDeclaration dataObjectClass = new CodeTypeDeclaration(Name);
            dataObjectClass.IsClass = true;
            dataObjectClass.IsPartial = true;
            dataObjectClass.TypeAttributes = TypeAttributes.Public;
            dataObjectClass.BaseTypes.Add(typeof(SystemShockObject<>).MakeGenericType(instanceType));
            dataObjectClass.CustomAttributes.Add(new CodeAttributeDeclaration(@"Serializable"));
            /*
            dataObjectClass.Members.Add(new CodeMemberField(instanceType, @"ClassData") {
                Attributes = MemberAttributes.Public | MemberAttributes.Override
            });
            */
            #region SetProperties
            CodeMemberMethod setProperties = new CodeMemberMethod() {
                Name = @"SetClassData",
                Attributes = MemberAttributes.Family | MemberAttributes.Override
            };
            setProperties.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), @"classData"));
            setProperties.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"ClassData"), new CodeCastExpression(instanceType, new CodeArgumentReferenceExpression(@"classData"))));

            dataObjectClass.Members.Add(setProperties);
            #endregion

            ns.Types.Add(dataObjectClass);

            compileUnit.Namespaces.Add(ns);

            CodeDomProvider codeProvider = CodeDomProvider.CreateProvider(@"CSharp");
            CodeGeneratorOptions codeGeneratorOptions = new CodeGeneratorOptions();
            using (StreamWriter sourceWriter = new StreamWriter(filePath))
                codeProvider.GenerateCodeFromCompileUnit(compileUnit, sourceWriter, codeGeneratorOptions);
        }
    }
}