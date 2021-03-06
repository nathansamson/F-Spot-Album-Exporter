//
// Mono.Tabblo.UploadProgressEventArgs
//
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Wojciech Dzierzanowski (wojciech.dzierzanowski@gmail.com)
//
// (C) Copyright 2006 Novell, Inc. (http://www.novell.com)
// (C) Copyright 2008 Wojciech Dzierzanowski
//

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

namespace Mono.Tabblo {

	public sealed class UploadProgressEventArgs : EventArgs {

		private readonly string title;
		private readonly long bytes_sent;
		private readonly long bytes_total;

		internal UploadProgressEventArgs (string title, long bytes_sent,
		                                  long bytes_total)
		{
			this.title = title;
			this.bytes_sent = bytes_sent;
			this.bytes_total = bytes_total;
		}

		public string Title {
			get {
				return title;
			}
		}

		public long BytesSent {
			get {
				return bytes_sent;
			}
		}

		public long BytesTotal {
			get {
				return bytes_total;
			}
		}
	}
}
