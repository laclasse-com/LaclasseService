using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Laclasse.Doc
{
    public class Image : Document
    {
        public static Dictionary<string, string> MimeToExtension = new Dictionary<string, string>()
        {
            ["image/jpeg"] = "jpg",
            ["image/png"] = "png",
            ["image/bmp"] = "bmp",
            ["image/gif"] = "gif",
            ["image/svg+xml"] = "svg",
            ["image/tiff"] = "tiff",
            ["image/targa"] = "tga",
            ["image/webp"] = "webp"
        };

        public Image(Context context, Node node) : base(context, node)
        {
        }

        public async Task<Stream> GetWebImageStreamAsync()
        {
            await node.blob.LoadExpandFieldAsync(context.db, "children");

            var imageBlob = node.blob.children.Find(child => child.name == "webimage");
            if (imageBlob != null)
                return context.blobs.GetBlobStream(imageBlob.id);

            Stream imageStream = null;
            var stream = await GetContentAsync();
            if (stream != null)
            {
                using (stream)
                {
                    var tempFile = Path.Combine(context.tempDir, Guid.NewGuid().ToString());
                    if (MimeToExtension.ContainsKey(node.mime))
                        tempFile += "." + MimeToExtension[node.mime];

                    using (var tmpStream = File.OpenWrite(tempFile))
                        await stream.CopyToAsync(tmpStream);
                    try
                    {
                        string thumbnailTempFile = null;
                        Blob thumbnailBlob = new Blob {
                            id = Guid.NewGuid().ToString(),
                            parent_id = node.blob_id,
                            mimetype = "image/jpeg",
                            name = "webimage"
                        };
                        Preview.PreviewFormat previewFormat;
                        string error;

                        var preview = new Preview.ImageVideoPreview(context.tempDir);
                        thumbnailTempFile = preview.Process(tempFile, node.mime, 2000, 2000, out previewFormat, out error);

                        if (thumbnailTempFile != null)
                        {
                            thumbnailBlob = await context.blobs.CreateBlobFromTempFileAsync(context.db, thumbnailBlob, thumbnailTempFile);
                            imageStream = context.blobs.GetBlobStream(thumbnailBlob.id);
                        }
                    }
                    finally
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            return imageStream;
        }
    }
}