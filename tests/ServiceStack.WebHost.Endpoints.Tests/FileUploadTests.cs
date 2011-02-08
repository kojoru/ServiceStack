using System;
using System.IO;
using System.Net;
using NUnit.Framework;
using ServiceStack.Common.Utils;
using ServiceStack.Common.Web;
using ServiceStack.Service;
using ServiceStack.ServiceClient.Web;
using ServiceStack.Text;
using ServiceStack.WebHost.Endpoints.Tests.Support.Host;
using ServiceStack.WebHost.Endpoints.Tests.Support.Services;

namespace ServiceStack.WebHost.Endpoints.Tests
{
	[TestFixture]
	public class FileUploadTests
	{
		public const string ListeningOn = "http://localhost:8080/";
		ExampleAppHostHttpListener appHost;

		[TestFixtureSetUp]
		public void TextFixtureSetUp()
		{
			appHost = new ExampleAppHostHttpListener();
			appHost.Init();
			appHost.Start(ListeningOn);
		}

		[TestFixtureTearDown]
		public void TestFixtureTearDown()
		{
			if (appHost != null) appHost.Dispose();
			appHost = null;
		}

		public void AssertResponse<T>(HttpWebResponse response, Action<T> customAssert)
		{
			var contentType = response.ContentType;

			AssertResponse(response, contentType);

			var contents = new StreamReader(response.GetResponseStream()).ReadToEnd();
			var result = DeserializeResult<T>(response, contents, contentType);

			customAssert(result);
		}

		private static T DeserializeResult<T>(WebResponse response, string contents, string contentType)
		{
			T result;
			switch (contentType)
			{
				case ContentType.Xml:
					result = XmlSerializer.DeserializeFromString<T>(contents);
					break;

				case ContentType.Json:
					result = JsonSerializer.DeserializeFromString<T>(contents);
					break;

				case ContentType.Jsv:
					result = TypeSerializer.DeserializeFromString<T>(contents);
					break;

				default:
					throw new NotSupportedException(response.ContentType);
			}
			return result;
		}

		public void AssertResponse(HttpWebResponse response, string contentType)
		{
			var statusCode = (int)response.StatusCode;
			Assert.That(statusCode, Is.LessThan(400));
			Assert.That(response.ContentType.StartsWith(contentType));
		}

		[Test]
		public void Can_POST_upload_file()
		{
			var uploadFile = new FileInfo("~/TestExistingDir/upload.html".MapAbsolutePath());

			var webRequest = (HttpWebRequest)WebRequest.Create(ListeningOn + "/fileuploads");
			var webResponse = webRequest.UploadFile(uploadFile, MimeTypes.GetMimeType(uploadFile.Name));

			AssertResponse<FileUploadResponse>((HttpWebResponse)webResponse, r =>
			{
				var expectedContents = new StreamReader(uploadFile.OpenRead()).ReadToEnd();
				Assert.That(r.FileName, Is.EqualTo(uploadFile.Name));
				Assert.That(r.ContentLength, Is.EqualTo(uploadFile.Length));
				Assert.That(r.ContentType, Is.EqualTo(MimeTypes.GetMimeType(uploadFile.Name)));
				Assert.That(r.Contents, Is.EqualTo(expectedContents));
			});
		}

		[Test]
		public void Can_POST_upload_file_using_ServiceClient()
		{
			IServiceClient client = new JsonServiceClient(ListeningOn);

			var uploadFile = new FileInfo("~/TestExistingDir/upload.html".MapAbsolutePath());


			var response = client.PostFile<FileUploadResponse>(
				ListeningOn + "/fileuploads", uploadFile, MimeTypes.GetMimeType(uploadFile.Name));


			var expectedContents = new StreamReader(uploadFile.OpenRead()).ReadToEnd();
			Assert.That(response.FileName, Is.EqualTo(uploadFile.Name));
			Assert.That(response.ContentLength, Is.EqualTo(uploadFile.Length));
			Assert.That(response.ContentType, Is.EqualTo(MimeTypes.GetMimeType(uploadFile.Name)));
			Assert.That(response.Contents, Is.EqualTo(expectedContents));
		}

		[Test]
		public void Can_handle_error_on_POST_upload_file_using_ServiceClient()
		{
			IServiceClient client = new JsonServiceClient(ListeningOn);

			var uploadFile = new FileInfo("~/TestExistingDir/upload.html".MapAbsolutePath());

			try
			{
				var response = client.PostFile<FileUploadResponse>(
					ListeningOn + "/fileuploads/ThrowError", uploadFile, MimeTypes.GetMimeType(uploadFile.Name));

				Assert.Fail("Upload Service should've thrown an error");
			}
			catch (Exception ex)
			{
				var webEx = ex as WebServiceException;
				var response = (FileUploadResponse)webEx.ResponseDto;
				Assert.That(response.ResponseStatus.ErrorCode, 
					Is.EqualTo(typeof(NotSupportedException).Name));
				Assert.That(response.ResponseStatus.Message, Is.EqualTo("ThrowError"));
			}
		}

		[Test]
		public void Can_GET_upload_file()
		{
			var uploadedFile = new FileInfo("~/TestExistingDir/upload.html".MapAbsolutePath());
			var webRequest = (HttpWebRequest)WebRequest.Create(ListeningOn + "/fileuploads/TestExistingDir/upload.html");
			var expectedContents = new StreamReader(uploadedFile.OpenRead()).ReadToEnd();

			var webResponse = webRequest.GetResponse();
			var actualContents = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();

			Assert.That(webResponse.ContentType, Is.EqualTo(MimeTypes.GetMimeType(uploadedFile.Name)));
			Assert.That(actualContents, Is.EqualTo(expectedContents));
		}

	}
}