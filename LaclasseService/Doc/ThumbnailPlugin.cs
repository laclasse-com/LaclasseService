using System;
namespace Laclasse.Doc
{
    public class ThumbnailPlugin
    {
		string name;
		int width;
		int height;

		public ThumbnailPlugin(string name, int width, int height)
        {
            this.name = name;
            if(this.name == null)
                this.name = "thumbnail";
            this.width = width;
            this.height = height;         
        }

		public string Name {
            get {
                return name;
            }
        }

        public string[] MimeTypes {
            get {
                return new string[] { "*/*" };
            }
        }      
    }   
}
