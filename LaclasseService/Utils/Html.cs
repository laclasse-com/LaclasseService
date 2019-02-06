using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Laclasse.Utils
{
    public static class Html
    {
        public static string RemoveScriptFromHtml(string html)
        {
            // load the document using sgml reader
            var document = new XmlDocument();
            using (var sgmlReader = new Sgml.SgmlReader())
            {
                sgmlReader.CaseFolding = Sgml.CaseFolding.ToLower;
                sgmlReader.DocType = "HTML";
                sgmlReader.WhitespaceHandling = WhitespaceHandling.None;

                using (var sr = new StringReader(html))
                {
                    sgmlReader.InputStream = sr;
                    document.Load(sgmlReader);
                }
            }
            // remove <script>
            var nodes = document.GetElementsByTagName("script");
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].ParentNode.RemoveChild(nodes[i]);

            RemoveAttributeScript(document.DocumentElement);

            return document.OuterXml;
        }

        static void RemoveAttributeScript(XmlNode node)
        {
            if (node.Attributes != null)
            {
                var removeList = new List<XmlAttribute>();
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (attribute.Name.StartsWith("on", StringComparison.InvariantCultureIgnoreCase))
                        removeList.Add(attribute);
                }
                foreach (var attribute in removeList)
                    node.Attributes.Remove(attribute);
            }

            foreach (XmlNode child in node.ChildNodes)
                RemoveAttributeScript(child);
        }
    }
}
