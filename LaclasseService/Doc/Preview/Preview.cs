// Preview.cs
// 
//  Generate Thumbnail for supported file formats
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2019 Metropole de Lyon
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 

namespace Laclasse.Doc.Preview
{
    public static class Preview
    {
        public static bool BuildPreview(string temporaryDirectory, string sourceFile, string mimetype, int width, int height, out string previewMimetype, out string previewPath, out string error)
        {
            IPreview preview = null;
            bool success = false;
            previewMimetype = null;
            previewPath = null;
            error = null;

            if (mimetype.StartsWith("image/", System.StringComparison.InvariantCulture) ||
                mimetype.StartsWith("video/", System.StringComparison.InvariantCulture) ||
                mimetype.StartsWith("audio/", System.StringComparison.InvariantCulture))
                preview = new ImageVideoPreview(temporaryDirectory);
            else if (mimetype == "application/pdf")
                preview = new PdfPreview(temporaryDirectory);
            else if (mimetype == "text/uri-list")
                preview = new UrlPreview(temporaryDirectory);
            //else if (mimetype == "application/x-laclasse-pad")
            //    preview = new HtmlPreview(temporaryDirectory);

            if (preview != null)
            {
                previewPath = preview.Process(sourceFile, mimetype, width, height, out PreviewFormat format, out error);
                if (previewPath != null)
                {
                    if (format == PreviewFormat.PNG)
                        previewMimetype = "image/png";
                    else
                        previewMimetype = "image/jpeg";
                    success = true;
                }
                else
                {
                    success = false;
                }
            }
            return success;
        }
    }
}
