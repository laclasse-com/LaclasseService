﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Laclasse.Doc {
    using System.Linq;
    using System.Text;
    using System.Collections.Generic;
    using System;
    
    
    public partial class OnlyOfficeView : OnlyOfficeViewBase {
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            
            #line 6 ""
            this.Write(@"<!DOCTYPE html>
<html>
    <head>
        <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
        <script type=""text/javascript"" src=""/onlyoffice/web-apps/apps/api/documents/api.js""></script>
<style>
html, body, .placeholder {
    width: 100%;
    height: 100%;
    position: absolute;
    top: 0px;
    bottom: 0px;
    left: 0px;
    margin: 0px;
    padding: 0px;
    overscroll-behavior-y: none;
}
</style>
    </head>
    <body>
        <div id=""placeholder""></div>
        <script>
var config = {
    ""document"": {
        ""fileType"": """);
            
            #line default
            #line hidden
            
            #line 30 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( fileType ));
            
            #line default
            #line hidden
            
            #line 30 ""
            this.Write("\",\n        \"key\": \"");
            
            #line default
            #line hidden
            
            #line 31 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( node.id + "REV" + node.rev ));
            
            #line default
            #line hidden
            
            #line 31 ""
            this.Write("\",\n        \"title\": \"");
            
            #line default
            #line hidden
            
            #line 32 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( node.name ));
            
            #line default
            #line hidden
            
            #line 32 ""
            this.Write("\",\n        \"url\": \"");
            
            #line default
            #line hidden
            
            #line 33 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( downloadUrl ));
            
            #line default
            #line hidden
            
            #line 33 ""
            this.Write("\",\n        \"permissions\": {\n            \"comment\": true,\n            \"download\": " +
                    "true,\n            \"edit\": ");
            
            #line default
            #line hidden
            
            #line 37 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( edit ? "true" : "false" ));
            
            #line default
            #line hidden
            
            #line 37 ""
            this.Write(",\n            \"print\": true,\n            \"review\": ");
            
            #line default
            #line hidden
            
            #line 39 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( edit ? "true" : "false" ));
            
            #line default
            #line hidden
            
            #line 39 ""
            this.Write("\n        }\n    },\n    \"documentType\": \"");
            
            #line default
            #line hidden
            
            #line 42 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( documentType ));
            
            #line default
            #line hidden
            
            #line 42 ""
            this.Write("\",\n    \"editorConfig\": {\n        \"callbackUrl\": \"");
            
            #line default
            #line hidden
            
            #line 44 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( callbackUrl ));
            
            #line default
            #line hidden
            
            #line 44 ""
            this.Write("\",\n        \"lang\": \"fr-FR\",\n        \"user\": {\n            \"id\": \"");
            
            #line default
            #line hidden
            
            #line 47 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( user.id ));
            
            #line default
            #line hidden
            
            #line 47 ""
            this.Write("\",\n            \"name\": \"");
            
            #line default
            #line hidden
            
            #line 48 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( user.firstname + " " + user.lastname ));
            
            #line default
            #line hidden
            
            #line 48 ""
            this.Write("\"\n        },\n        \"customization\": {\n            \"forcesave\": true,\n          " +
                    "  \"logo\": {\n                \"image\": \"/portail/img/logo-onlyoffice.png\"\n        " +
                    "    }\n        }\n    },\n    \"type\": \"");
            
            #line default
            #line hidden
            
            #line 57 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( mode == OnlyOfficeMode.Mobile ? "mobile" : "desktop" ));
            
            #line default
            #line hidden
            
            #line 57 ""
            this.Write("\",\n    \"width\": \"100%\",\n    \"height\": \"100%\"\n};\n\nvar docEditor = new DocsAPI.DocE" +
                    "ditor(\"placeholder\", config);\n        </script>\n    </body>\n</html>");
            
            #line default
            #line hidden
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class OnlyOfficeViewBase {
        
        private global::System.Text.StringBuilder builder;
        
        private global::System.Collections.Generic.IDictionary<string, object> session;
        
        private global::System.CodeDom.Compiler.CompilerErrorCollection errors;
        
        private string currentIndent = string.Empty;
        
        private global::System.Collections.Generic.Stack<int> indents;
        
        private ToStringInstanceHelper _toStringHelper = new ToStringInstanceHelper();
        
        public virtual global::System.Collections.Generic.IDictionary<string, object> Session {
            get {
                return this.session;
            }
            set {
                this.session = value;
            }
        }
        
        public global::System.Text.StringBuilder GenerationEnvironment {
            get {
                if ((this.builder == null)) {
                    this.builder = new global::System.Text.StringBuilder();
                }
                return this.builder;
            }
            set {
                this.builder = value;
            }
        }
        
        protected global::System.CodeDom.Compiler.CompilerErrorCollection Errors {
            get {
                if ((this.errors == null)) {
                    this.errors = new global::System.CodeDom.Compiler.CompilerErrorCollection();
                }
                return this.errors;
            }
        }
        
        public string CurrentIndent {
            get {
                return this.currentIndent;
            }
        }
        
        private global::System.Collections.Generic.Stack<int> Indents {
            get {
                if ((this.indents == null)) {
                    this.indents = new global::System.Collections.Generic.Stack<int>();
                }
                return this.indents;
            }
        }
        
        public ToStringInstanceHelper ToStringHelper {
            get {
                return this._toStringHelper;
            }
        }
        
        public void Error(string message) {
            this.Errors.Add(new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message));
        }
        
        public void Warning(string message) {
            global::System.CodeDom.Compiler.CompilerError val = new global::System.CodeDom.Compiler.CompilerError(null, -1, -1, null, message);
            val.IsWarning = true;
            this.Errors.Add(val);
        }
        
        public string PopIndent() {
            if ((this.Indents.Count == 0)) {
                return string.Empty;
            }
            int lastPos = (this.currentIndent.Length - this.Indents.Pop());
            string last = this.currentIndent.Substring(lastPos);
            this.currentIndent = this.currentIndent.Substring(0, lastPos);
            return last;
        }
        
        public void PushIndent(string indent) {
            this.Indents.Push(indent.Length);
            this.currentIndent = (this.currentIndent + indent);
        }
        
        public void ClearIndent() {
            this.currentIndent = string.Empty;
            this.Indents.Clear();
        }
        
        public void Write(string textToAppend) {
            this.GenerationEnvironment.Append(textToAppend);
        }
        
        public void Write(string format, params object[] args) {
            this.GenerationEnvironment.AppendFormat(format, args);
        }
        
        public void WriteLine(string textToAppend) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendLine(textToAppend);
        }
        
        public void WriteLine(string format, params object[] args) {
            this.GenerationEnvironment.Append(this.currentIndent);
            this.GenerationEnvironment.AppendFormat(format, args);
            this.GenerationEnvironment.AppendLine();
        }
        
        public class ToStringInstanceHelper {
            
            private global::System.IFormatProvider formatProvider = global::System.Globalization.CultureInfo.InvariantCulture;
            
            public global::System.IFormatProvider FormatProvider {
                get {
                    return this.formatProvider;
                }
                set {
                    if ((value != null)) {
                        this.formatProvider = value;
                    }
                }
            }
            
            public string ToStringWithCulture(object objectToConvert) {
                if ((objectToConvert == null)) {
                    throw new global::System.ArgumentNullException("objectToConvert");
                }
                global::System.Type type = objectToConvert.GetType();
                global::System.Type iConvertibleType = typeof(global::System.IConvertible);
                if (iConvertibleType.IsAssignableFrom(type)) {
                    return ((global::System.IConvertible)(objectToConvert)).ToString(this.formatProvider);
                }
                global::System.Reflection.MethodInfo methInfo = type.GetMethod("ToString", new global::System.Type[] {
                            iConvertibleType});
                if ((methInfo != null)) {
                    return ((string)(methInfo.Invoke(objectToConvert, new object[] {
                                this.formatProvider})));
                }
                return objectToConvert.ToString();
            }
        }
    }
}
