// HtmlPreview.cs
// 
//  Get an image preview of a HTML file
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

using System;
using System.IO;
using System.Diagnostics;

namespace Laclasse.Doc.Preview
{
    public class HtmlPreview : IPreview
    {
        const string webshotScript = @"
var page = require('webpage').create(),
    system = require('system'),
    htmlfile, output, size, delay;
var fs = require('fs');

if(system.args.length < 3 || system.args.length > 5) {
    console.log('Usage: webshot.js htmlfile filename [widthxheight] [delayms]');
    phantom.exit(1);
}
else {
    htmlfile = system.args[1];
    output = system.args[2];
    page.viewportSize = { width: 1024, height: 768 };
    if(system.args.length > 3) {
        size = system.args[3].split('x');
        page.zoomFactor = (new Number(size[0]))/1024;
        page.clipRect = { top: 0, left: 0, width: new Number(size[0]), height: new Number(size[1]) };
        page.viewportSize = { width: new Number(size[0]), height: new Number(size[1]) };
    }
    if(system.args.length > 4)
        delay = new Number(system.args[4]);
    page.content = fs.read(htmlfile);
    page.evaluate(function() {
      var style = document.createElement('style'),
          text = document.createTextNode('body { background: #fff }');
      style.setAttribute('type', 'text/css');
      style.appendChild(text);
      document.head.insertBefore(style, document.head.firstChild);
    });
    window.setTimeout(function () {
        page.render(output);
        phantom.exit();
    }, delay);
}
";

        string temporaryDirectory;

        public HtmlPreview(string temporaryDirectory)
        {
            this.temporaryDirectory = temporaryDirectory;
        }

        public string Process(string file, string mimetype, int width, int height, out PreviewFormat format, out string error)
        {
            error = null;
            format = PreviewFormat.JPEG;
            Console.WriteLine($"HtmlPreview.Process file: {file}");
            return BuildWebshot(temporaryDirectory, file, width, height);
        }

        static string BuildArguments(string[] args)
        {
            string res = "";
            foreach (string arg in args)
            {
                string tmp = (string)arg.Clone();
                tmp = tmp.Replace("'", "\\'");
                if (res != "")
                    res += " ";
                res += "'" + tmp + "'";
            }
            return res;
        }

        public static string BuildWebshot(string tmpDir, string url, int width, int height)
        {
            string fileId = Guid.NewGuid().ToString();
            string filename = tmpDir + "/" + fileId + ".jpg";

            string args = BuildArguments(new string[]{
                "--ignore-ssl-errors=yes",
                "--ssl-protocol=any",
                "/dev/stdin",
                url,
                filename,
                width+"x"+height,
                "200"
            });

            ProcessStartInfo startInfo = new ProcessStartInfo("/usr/bin/phantomjs", args);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.UseShellExecute = false;
            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            // write the JS script to stdin
            process.StandardInput.Write(webshotScript);
            process.StandardInput.Close();

            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Dispose();

            if (exitCode != 0)
                return null;
            return filename;
        }
    }
}
