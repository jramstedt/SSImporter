using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.CodeDom;
using System.CodeDom.Compiler;

using SystemShock.Object;
using SystemShock.Resource;

namespace SSImporter.Resource {
    public class DataObjectGenerator {
        [MenuItem("Assets/System Shock/2. Generate DataObject Classes", false, 1002)]
        public static void Init() {
            ObjectDeclaration[][] ObjectDeclarations = ObjectPropertyImport.ObjectDeclarations;

            try {
                AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(Application.dataPath + @"/SystemShock"))
                    AssetDatabase.CreateFolder(@"Assets", @"SystemShock");

                if (!Directory.Exists(Application.dataPath + @"/SystemShock/DataObjects"))
                    AssetDatabase.CreateFolder(@"Assets/SystemShock", @"DataObjects");

                for (uint classIndex = 0; classIndex < ObjectDeclarations.Length; ++classIndex) {
                    ObjectDeclaration[] objectDataSubclass = ObjectDeclarations[classIndex];

                    for (uint subclassIndex = 0; subclassIndex < objectDataSubclass.Length; ++subclassIndex) {
                        ObjectDeclaration objectDataType = objectDataSubclass[subclassIndex];

                        GenerateDataObjectClass(objectDataType.GetGenericType(), objectDataType.GetSpecificType());
                        GenerateMonoBehaviourClass(objectDataType.GetGenericType(), objectDataType.GetSpecificType());
                    }
                }
            } finally {
                AssetDatabase.StopAssetEditing();
                EditorApplication.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/System Shock/2. Generate DataObject Classes", true)]
        public static bool Validate() {
            return PlayerPrefs.HasKey(@"SSHOCKRES");
        }

        public static void GenerateDataObjectClass(Type genericType, Type specificType) {
            string Name = genericType.Name + specificType.Name;
            
            string fullNamespace = @"SystemShock.DataObjects";
            //string fullName = fullNamespace + "." + Name;
            string filePath = Application.dataPath + @"/SystemShock/DataObjects/" + Name + @".cs";

            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeNamespace globalNs = new CodeNamespace();
            globalNs.Imports.Add(new CodeNamespaceImport(@"System"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"SystemShock.Resource"));

            compileUnit.Namespaces.Add(globalNs);

            CodeNamespace ns = new CodeNamespace(fullNamespace);

            #region ScriptableObject
            CodeTypeDeclaration dataObjectClass = new CodeTypeDeclaration(Name);
            dataObjectClass.IsClass = true;
            dataObjectClass.IsPartial = true;
            dataObjectClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            dataObjectClass.BaseTypes.Add(typeof(ObjectData));

            /*dataObjectClass.Members.Add(new CodeMemberField(baseType, @"Base") {
                Attributes = MemberAttributes.Public
            });*/

            dataObjectClass.Members.Add(new CodeMemberField(genericType, @"Generic") {
                Attributes = MemberAttributes.Public
            });

            dataObjectClass.Members.Add(new CodeMemberField(specificType, @"Specific") {
                Attributes = MemberAttributes.Public
            });
            /*
            #region SetBase
            CodeMemberMethod setBase = new CodeMemberMethod() {
                Name = @"SetBase",
                Attributes = MemberAttributes.Public | MemberAttributes.Override
            };
            setBase.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), @"@base"));
            setBase.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Base"), new CodeCastExpression(baseType, new CodeArgumentReferenceExpression(@"@base"))));

            dataObjectClass.Members.Add(setBase);
            #endregion
            */
            #region SetGeneric
            CodeMemberMethod setGeneric = new CodeMemberMethod() {
                Name = @"SetGeneric",
                Attributes = MemberAttributes.Public | MemberAttributes.Override
            };
            setGeneric.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), @"generic"));
            setGeneric.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Generic"), new CodeCastExpression(genericType, new CodeArgumentReferenceExpression(@"generic"))));

            dataObjectClass.Members.Add(setGeneric);
            #endregion

            #region SetSpecific
            CodeMemberMethod setSpecific = new CodeMemberMethod() {
                Name = @"SetSpecific",
                Attributes = MemberAttributes.Public | MemberAttributes.Override
            };
            setSpecific.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), @"specific"));
            setSpecific.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Specific"), new CodeCastExpression(specificType, new CodeArgumentReferenceExpression(@"specific"))));

            dataObjectClass.Members.Add(setSpecific);
            #endregion

            ns.Types.Add(dataObjectClass);
            #endregion

            compileUnit.Namespaces.Add(ns);

            CodeDomProvider codeProvider = CodeDomProvider.CreateProvider(@"CSharp");
            CodeGeneratorOptions codeGeneratorOptions = new CodeGeneratorOptions();
            using (StreamWriter sourceWriter = new StreamWriter(filePath))
                codeProvider.GenerateCodeFromCompileUnit(compileUnit, sourceWriter, codeGeneratorOptions);

            /*
             CompilerParameters compilerParameters = new CompilerParameters() {
                GenerateExecutable = false,
                GenerateInMemory = true,
                TreatWarningsAsErrors = true
            };

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                compilerParameters.ReferencedAssemblies.Add(assembly.Location);

            CompilerResults result = codeProvider.CompileAssemblyFromDom(compilerParameters, compileUnit);

            if (result.Errors.HasErrors) {
                foreach (string line in result.Output)
                    Debug.LogError(line);
            }
            */
        }

        public static void GenerateMonoBehaviourClass(Type genericType, Type specificType) {
            string Name = genericType.Name + specificType.Name;

            string fullNamespace = @"SystemShock.DataObjects";
            //string fullName = fullNamespace + "." + Name;
            string filePath = Application.dataPath + @"/SystemShock/DataObjects/" + Name + @"MonoBehaviour.cs";

            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeNamespace globalNs = new CodeNamespace();
            globalNs.Imports.Add(new CodeNamespaceImport(@"System"));
            globalNs.Imports.Add(new CodeNamespaceImport(@"SystemShock.Resource"));

            compileUnit.Namespaces.Add(globalNs);

            CodeNamespace ns = new CodeNamespace(fullNamespace);

            #region MonoBehaviour
            CodeTypeDeclaration monoBehaviourClass = new CodeTypeDeclaration(Name + @"MonoBehaviour");
            monoBehaviourClass.IsClass = true;
            monoBehaviourClass.IsPartial = true;
            monoBehaviourClass.TypeAttributes = TypeAttributes.Public;
            monoBehaviourClass.BaseTypes.Add(typeof(SystemShockObjectProperties<,>).MakeGenericType(genericType, specificType));
            monoBehaviourClass.CustomAttributes.Add(new CodeAttributeDeclaration(@"Serializable"));

            monoBehaviourClass.Members.Add(new CodeMemberField(Name, @"Properties") {
                Attributes = MemberAttributes.Public
            });

            #region SetProperties
            CodeMemberMethod setProperties = new CodeMemberMethod() {
                Name = @"SetProperties",
                Attributes = MemberAttributes.Public | MemberAttributes.Override
            };
            setProperties.Parameters.Add(new CodeParameterDeclarationExpression(typeof(ObjectData), @"properties"));
            setProperties.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Properties"), new CodeCastExpression(Name, new CodeArgumentReferenceExpression(@"properties"))));

            monoBehaviourClass.Members.Add(setProperties);
            #endregion
            
            #region Base property
            CodeMemberProperty baseProperty = new CodeMemberProperty() {
                Name = @"Base",
                Type = new CodeTypeReference(typeof(BaseProperties)),
                Attributes = MemberAttributes.Public |  MemberAttributes.Override,
                HasGet = true,
                HasSet = false
            };
            baseProperty.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Properties"), @"Base")));
            monoBehaviourClass.Members.Add(baseProperty);
            #endregion
            
            #region Generic property
            CodeMemberProperty genericProperty = new CodeMemberProperty() {
                Name = @"Generic",
                Type = new CodeTypeReference(genericType),
                Attributes = MemberAttributes.Public | MemberAttributes.Override,
                HasGet = true,
                HasSet = false
            };
            genericProperty.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Properties"), @"Generic")));
            monoBehaviourClass.Members.Add(genericProperty);
            #endregion

            #region Specific property
            CodeMemberProperty specificProperty = new CodeMemberProperty() {
                Name = @"Specific",
                Type = new CodeTypeReference(specificType),
                Attributes = MemberAttributes.Public | MemberAttributes.Override,
                HasGet = true,
                HasSet = false
            };
            specificProperty.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), @"Properties"), @"Specific")));
            monoBehaviourClass.Members.Add(specificProperty);
            #endregion

            ns.Types.Add(monoBehaviourClass);
            #endregion

            compileUnit.Namespaces.Add(ns);

            CodeDomProvider codeProvider = CodeDomProvider.CreateProvider(@"CSharp");
            CodeGeneratorOptions codeGeneratorOptions = new CodeGeneratorOptions();
            using (StreamWriter sourceWriter = new StreamWriter(filePath))
                codeProvider.GenerateCodeFromCompileUnit(compileUnit, sourceWriter, codeGeneratorOptions);
        }
    }
}