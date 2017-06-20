﻿// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.42000
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------

namespace Laclasse.Authentication {
    using System.Linq;
    using System.Text;
    using System.Collections.Generic;
    using Erasme.Http;
    using System;
    
    
    public partial class CasView : CasViewBase {
        
        public virtual string TransformText() {
            this.GenerationEnvironment = null;
            
            #line 1 ""
            this.Write("﻿");
            
            #line default
            #line hidden
            
            #line 7 ""
            this.Write("<!DOCTYPE html>\n<html>\n\t<head>\n\t\t<title>Service d'Authentification Central de laclasse.com</title>\n\t\t\t<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/>\n\t\t\t<meta name=\"apple-mobile-web-app-capable\" content=\"yes\">\n\t\t\t<meta name=\"mobile-web-app-capable\" content=\"yes\">\n\t\t\t<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">\n\t\t\t<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no\">\n\t\t\t<style>\nbody {\n\tcolor: white;\n\tbackground-color: #1aaacc;\n\tfont-family: \"Open Sans\", sans-serif;\n\tfont-size: 20px;\n}\n\na {\n\tcolor: white;\n}\n\n.logo {\n\twidth: 55%;\n\topacity: 0.2;\n\tposition: absolute;\n\tleft: -5%;\n\ttop: -5%;\n\t-webkit-user-select: none;\n}\n\n.footer {\n\twidth: 75%;\n\tdisplay: inline-block;\n\tmargin-top: 50px;\n\tmargin-bottom: 50px;\n}\n\n.btn {\n\tdisplay: inline-block;\n\tfont-size: 16px;\n\ttext-transform: uppercase;\n\tpadding: 10px 20px;\n\tborder: 1px solid white;\n\tborder-radius: 0;\n\tbackground-color: #5bc0de;\n\tmargin: 5px;\n    color: white;\n\twhite-space: nowrap;\n\ttext-decoration: none;\n\tcursor: pointer;\n}\n\n.btn:hover {\n\tbackground-color: rgba(91,192,222,0);\n}\n\n.box {\n\tmargin: 20px;\n\tfloat: right;\n    background: rgba(255,255,255,0.2);\n    padding: 20px;\n}\n\ninput[type=text], input[type=password] {\n    height: 30px;\n    border: 1px solid white;\n    background-color: rgba(255,255,255,0.3);\n    margin: 5px;\n    color: white;\n    font-size: 18px;\n    padding-left: 10px;\n    padding-right: 10px;\n}\n\n.title {\n    font-weight: bold;\n    margin-bottom: 20px;\n}\n\t\t</style>\n\t\t<script>\nfunction onRescue()\n{\n\terrorContent = document.getElementById(\"error-content\");\n\tif (errorContent != null)\n\t\terrorContent.style.display = \"none\";\n\tdocument.getElementById(\"authentication-content\").style.display = \"none\";\n\tdocument.getElementById(\"rescue-content\").style.display = \"inherit\";\n}\n\nfunction onRescueBack(e)\n{\n\tdocument.getElementById(\"authentication-content\").style.display = \"inherit\";\n\tdocument.getElementById(\"rescue-content\").style.display = \"none\";\n}\n\t\t</script>\n\t</head>\n<body>\n\t\t\t<img draggable=\"false\" class=\"logo\" src=\"images/logolaclasse.svg\" alt=\"Logo ENT\">\n\t\t\t<div style=\"position: absolute; top: 0px; left: 0px; right: 0px; bottom: 0px;\">\n\t\t\t<center>\n\t\t\t\t<div style=\"max-width: 1200px\">\n\t\t\t\t\t<div style=\"text-align: center; max-width: 400px; padding: 40px; padding-top: 100px; padding-bottom: 100px; float: left;\">\n\t\t\t\t\t\t<div style=\"font-weight: bold; font-size: 34px\">Laclasse.com</div><br>\n\t\t\t\t\t\tEspace Numérique de Travail<br>\n\t\t\t\t\t\tdes collèges et écoles de la Métropole de Lyon.\n\t\t\t\t\t\t<p>\n\t\t\t\t\t\t\t<strong>Besoin d'aide ?</strong>\n\t\t\t\t\t\t\t<ul style=\"text-align:left\">\n\t\t\t\t\t\t\t\t<li>\n\t\t\t\t\t\t\t\t\t<a href=\"http://ent-laclasse.blogs.laclasse.com\">Consulter le blog de l'ENT</a>\n\t\t\t\t\t\t\t\t</li>\n\t\t\t\t\t\t\t\t<li>\n\t\t\t\t\t\t\t\t\tsi vous êtes parent, élève ou personnel contactez votre administrateur d'établissement\n\t\t\t\t\t\t\t\t</li>\n\t\t\t\t\t\t\t\t<li>\n\t\t\t\t\t\t\t\t\tsi vous êtes administrateur d'établissement sur le territoire de la Métropole de Lyon contactez le SVP Métropole par courriel : <a href=\"mailto:svp4356@grandlyon.com\">svp4356@grandlyon.com</a> ou par téléphone : 04.78.63.43.56\n\t\t\t\t\t\t\t\t</li>\n\t\t\t\t\t\t\t\t<li>sinon prenez contact avec votre collectivité de rattachement</li>\n\t\t\t\t\t\t\t</ul>\n\t\t\t\t\t\t</p>\n\t\t\t\t\t</div>\n\n\n\t\t\t\t\t<div class=\"box\" style=\"max-width: 400px; text-align: left\">\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 131 ""
 if (error != null) { 
            
            #line default
            #line hidden
            
            #line 132 ""
            this.Write("\n\t\t\t\t\t\t<div style=\"display: inherit\" id=\"error-content\">\n\t\t\t\t\t\t\t<div style=\"font-size: 30px; text-align: center; margin-bottom: 10px; padding: 10px; color: white; background-color: #eb5454;\">\n\t\t\t\t\t\t\t\tErreur\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px; color: #eb5454\">\n\t\t\t\t\t\t\t\t<div>\n\t\t\t\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 139 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( error ));
            
            #line default
            #line hidden
            
            #line 139 ""
            this.Write("\n\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t</div>\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 143 ""
 } 
            
            #line default
            #line hidden
            
            #line 144 ""
            this.Write("\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 145 ""
 if (message != null) { 
            
            #line default
            #line hidden
            
            #line 146 ""
            this.Write("\n\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t<div class=\"title\">");
            
            #line default
            #line hidden
            
            #line 148 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( title ));
            
            #line default
            #line hidden
            
            #line 148 ""
            this.Write("</div>\n\t\t\t\t\t\t\t<div>\n\t\t\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 150 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( message ));
            
            #line default
            #line hidden
            
            #line 150 ""
            this.Write("\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t<a class=\"btn\" href=\"logout\">SE DÉCONNECTER</a>\n\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 155 ""

						}
						else
						{
							if (rescueUsers != null) { 
            
            #line default
            #line hidden
            
            #line 160 ""
            this.Write("\n\t\t\t\t\t\t<div id=\"rescue-content\">\n\t\t\t\t\t\t\t<div style=\"font-size: 30px; text-align: center; margin-bottom: 10px; padding: 10px; color: white; background-color: #1aaacc;\">\n\t\t\t\t\t\t\t\t<a href=\"#back\" onclick=\"onRescueBack()\" style=\"font-weight: bold; text-decoration: none; float: left\">&#8592;</a> Mot de passe perdu\n\t\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t\t<div class=\"title\">Choisissez l'utilisateur pour lequel vous avez perdu le mot de passe</div>\n\n\t\t\t\t\t\t\t\t<form method=\"post\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"service\" value=\"");
            
            #line default
            #line hidden
            
            #line 170 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( service ));
            
            #line default
            #line hidden
            
            #line 170 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"ticket\" value=\"");
            
            #line default
            #line hidden
            
            #line 171 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( ticket ));
            
            #line default
            #line hidden
            
            #line 171 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"rescue\" value=\"");
            
            #line default
            #line hidden
            
            #line 172 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( rescue ));
            
            #line default
            #line hidden
            
            #line 172 ""
            this.Write("\">\n\t\t    \t        \t\t\t");
            
            #line default
            #line hidden
            
            #line 173 ""
 var first = "checked"; foreach (var user in rescueUsers) { 
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write("\t\t    \t        \t\t\t<input type=\"radio\" name=\"user\" value=\"");
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( user.id ));
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write("\" ");
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( first ));
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write(">");
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( user.firstname + " " + user.lastname ));
            
            #line default
            #line hidden
            
            #line 174 ""
            this.Write("</input><br>\n\t\t\t\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 175 ""
 first = ""; } 
            
            #line default
            #line hidden
            
            #line 176 ""
            this.Write("\t\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\t<input class=\"btn\" name=\"submit\" type=\"submit\" value=\"VALIDER\">\n\t\t\t\t\t\t\t\t</form>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 182 ""
 	} else if (rescueId != null) { 
            
            #line default
            #line hidden
            
            #line 183 ""
            this.Write("\n\t\t\t\t\t\t<div id=\"rescue-content\">\n\t\t\t\t\t\t\t<div style=\"font-size: 30px; text-align: center; margin-bottom: 10px; padding: 10px; color: white; background-color: #1aaacc;\">\n\t\t\t\t\t\t\t\t<a href=\"#back\" onclick=\"onRescueBack()\" style=\"font-weight: bold; text-decoration: none; float: left\">&#8592;</a> Mot de passe perdu\n\t\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t\t<div>\n\t\t\t\t\t\t\t\t\tUn code vient de vous être envoyé à <b>");
            
            #line default
            #line hidden
            
            #line 191 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( rescue ));
            
            #line default
            #line hidden
            
            #line 191 ""
            this.Write("</b>. Merci de le saisir dans\n\t\t\t\t\t\t\t\t\tle champ ci-dessous. Cela vous permettra de vous connecter sur le compte\n\t\t\t\t\t\t\t\t\tde ");
            
            #line default
            #line hidden
            
            #line 193 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( rescueUser ));
            
            #line default
            #line hidden
            
            #line 193 ""
            this.Write(". Une fois connecté, pensez bien à changer le mot de passe.\n\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t<form method=\"post\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"service\" value=\"");
            
            #line default
            #line hidden
            
            #line 197 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( service ));
            
            #line default
            #line hidden
            
            #line 197 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"ticket\" value=\"");
            
            #line default
            #line hidden
            
            #line 198 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( ticket ));
            
            #line default
            #line hidden
            
            #line 198 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"rescue\" value=\"");
            
            #line default
            #line hidden
            
            #line 199 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( rescue ));
            
            #line default
            #line hidden
            
            #line 199 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"rescueId\" value=\"");
            
            #line default
            #line hidden
            
            #line 200 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( rescueId ));
            
            #line default
            #line hidden
            
            #line 200 ""
            this.Write("\">\n\t\t    \t        \t\t\t<div>Code:</div>\n\t\t    \t        \t\t\t<input type=\"text\" name=\"rescueCode\" value=\"\">\n\t\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\t<input class=\"btn\" name=\"submit\" type=\"submit\" value=\"VALIDER\">\n\t\t\t\t\t\t\t\t</form>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 209 ""
 	} 
            
            #line default
            #line hidden
            
            #line 210 ""
            this.Write("\n\t\t\t\t\t\t<!-- authentication -->\n\t\t\t\t\t\t<div style=\"display: ");
            
            #line default
            #line hidden
            
            #line 212 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( (rescue != null) ? "none" : "inherit" ));
            
            #line default
            #line hidden
            
            #line 212 ""
            this.Write("\" id=\"authentication-content\">\n\t\t\t\t\t\t\t<div style=\"font-size: 30px; text-align: center; margin-bottom: 10px; padding: 10px; color: white; background-color: #1aaacc;\">\n\t\t\t\t\t\t\t\tAuthentification\n\t\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t\t<div class=\"title\">Connectez-vous avec votre compte Académique.</div>\n\t\t\t\t\t\t\t\t<div>\n\t\t\t\t\t\t\t\t\t<a class=\"btn\" href=\"parentPortalIdp?service=");
            
            #line default
            #line hidden
            
            #line 220 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( HttpUtility.UrlEncode(service) ));
            
            #line default
            #line hidden
            
            #line 220 ""
            this.Write("\">Parents/Elèves</a>\n\t\t\t\t\t\t\t\t\t<a class=\"btn\" href=\"agentPortalIdp?service=");
            
            #line default
            #line hidden
            
            #line 221 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( HttpUtility.UrlEncode(service) ));
            
            #line default
            #line hidden
            
            #line 221 ""
            this.Write("\">Profs/Agents</a>\n\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t<div style=\"height: 2px; background-color: #fff; text-align: center; margin-bottom: 1em\">\n\t\t\t\t\t\t\t\t<span style=\"background-color: #48bbd6; position: relative; top: -0.5em; margin: 0px auto;font-weight: bold\">&nbsp;OU&nbsp;</span>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t<br>\n\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t\t<div class=\"title\">Connectez-vous avec votre compte Laclasse.com.</div>\n\t\t\t\t\t\t\t\t<form method=\"post\" action=\"?\">\n\t\t\t            \t\t\t<input type=\"hidden\" name=\"service\" value=\"");
            
            #line default
            #line hidden
            
            #line 233 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( service ));
            
            #line default
            #line hidden
            
            #line 233 ""
            this.Write("\">\n\t\t\t            \t\t\t<input type=\"hidden\" name=\"ticket\" value=\"");
            
            #line default
            #line hidden
            
            #line 234 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( ticket ));
            
            #line default
            #line hidden
            
            #line 234 ""
            this.Write("\">\n\t\t\t\t\t\t\t\t\t<div>Identifiant:</div>\n\t\t\t\t\t\t\t\t\t<input name=\"username\" type=\"text\" style=\"width: 80%; margin-bottom: 10px;\">\n\t\t\t\t\t\t\t\t\t<div>Mot de passe:</div>\n\t\t\t\t\t\t\t\t\t<input name=\"password\" type=\"password\" style=\"width: 80%; margin-bottom: 10px;\">\n\t\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\t<input class=\"btn\" name=\"submit\" type=\"submit\" value=\"SE CONNECTER\">\n\t\t\t\t\t\t\t\t</form>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t<a href=\"#\" onclick=\"onRescue()\">Mot de passe oublié ?</a>\n\t\t\t\t\t\t</div>\n\n\t   \t\t\t\t\t<!-- lost password -->\n\t\t\t\t\t\t<div style=\"display: none\" id=\"rescue-content\">\n\t\t\t\t\t\t\t<div style=\"font-size: 30px; text-align: center; margin-bottom: 10px; padding: 10px; color: white; background-color: #1aaacc;\">\n\t\t\t\t\t\t\t\t<a href=\"#back\" onclick=\"onRescueBack()\" style=\"font-weight: bold; text-decoration: none; float: left\">&#8592;</a> Mot de passe perdu\n\t\t\t\t\t\t\t</div>\n\n\t\t\t\t\t\t\t<div style=\"margin-bottom: 20px;\">\n\t\t\t\t\t\t\t\t<div>\n\t\t\t\t\t\t\t\t\tMerci de renseigner une adresse email (autre que celle de l'ENT) ou numéro de\n\t\t\t\t\t\t\t\t\ttéléphone portable: les vôtres ou ceux d'un de vos parents.<br>\n\t\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\tPar exemple:\n\t\t\t\t\t\t\t\t\t<ul>\n\t\t\t\t\t\t\t\t\t\t<li>pour un compte enseignant, votre adresse email académique.\n\t\t\t\t\t\t\t\t\t\t<li>pour un élève, l'adresse email de votre mère.\n\t\t\t\t\t\t\t\t\t\t<li>pour un parent, votre numéro de téléphone portable que vous avez communiqué lors de l'inscription\n\t\t\t\t\t\t\t\t\t</ul>\n\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t\t<form method=\"post\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"service\" value=\"");
            
            #line default
            #line hidden
            
            #line 265 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( service ));
            
            #line default
            #line hidden
            
            #line 265 ""
            this.Write("\">\n\t\t    \t        \t\t\t<input type=\"hidden\" name=\"ticket\" value=\"");
            
            #line default
            #line hidden
            
            #line 266 ""
            this.Write(this.ToStringHelper.ToStringWithCulture( ticket ));
            
            #line default
            #line hidden
            
            #line 266 ""
            this.Write("\">\n\t\t\t\t\t\t\t\t\t<input name=\"rescue\" type=\"text\" style=\"width: 80%; margin-bottom: 10px;\">\n\t\t\t\t\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\t<input class=\"btn\" name=\"submit\" type=\"submit\" value=\"RÉCUPÉRER\">\n\t\t\t\t\t\t\t\t</form>\n\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t</div>\n\t\t\t\t\t\t");
            
            #line default
            #line hidden
            
            #line 273 ""
 } 
            
            #line default
            #line hidden
            
            #line 274 ""
            this.Write("\t\t\t\t\t</div>\n\t\t\t\t</div>\n\n\t\t\t\t<div class=\"footer\">\n\t\t\t\t\t<img draggable=\"false\" style=\"width: 40%\" src=\"images/grandlyon-logo-blanc.svg\" alt=\"Logo Métropole du Grand Lyon\" />\n\t\t\t\t\t<img draggable=\"false\" style=\"width: 25%\" src=\"images/logo-academie-blanc.svg\" alt=\"Logo Académie de Lyon\" />\n\t\t\t\t</div>\n\t\t\t</center>\n\t\t</div>\n\t</body>\n</html>");
            
            #line default
            #line hidden
            return this.GenerationEnvironment.ToString();
        }
        
        public virtual void Initialize() {
        }
    }
    
    public class CasViewBase {
        
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
