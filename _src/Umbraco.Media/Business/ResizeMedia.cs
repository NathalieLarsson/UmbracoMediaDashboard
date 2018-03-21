using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Results;
using ImageProcessor;
using ImageProcessor.Imaging;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web.WebApi;

namespace Umbraco.Media.Business
{
	public class ResizeMediaController : UmbracoApiController
	{
		[HttpGet]
		public HttpResponseMessage ResizeAll()
		{
			var count = 0;
			long size = 0;
			var stopwatch = new Stopwatch();
			var images = GetAllImages();
			var oldSize = images.Select(x => x.GetValue<long>("umbracoBytes")).Sum();
			var imagesCount = images.Count;
			var resizedImages = new List<MediaResizeItem>();

			stopwatch.Start();

			MediaFileSystem mediaFileSystem = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();
			IContentSection contentSection = UmbracoConfig.For.UmbracoSettings().Content;
			IEnumerable<string> supportedTypes = contentSection.ImageFileTypes.ToList();

			foreach (var image in images)
			{
				var pathStr = image.GetValue<string>("umbracoFile");

				if(string.IsNullOrEmpty(pathStr)) continue;

				var path = pathStr.Contains("{") ? JsonConvert.DeserializeObject<UmbracoPath>(pathStr).Src : pathStr;

				if(string.IsNullOrEmpty(path)) continue;

				var extension = Path.GetExtension(path).Substring(1);

				if (!supportedTypes.InvariantContains(extension)) continue;

				try
				{
					var resizedImage = ResizeImage(image, path, mediaFileSystem, count, imagesCount, out var addedSize);
					
					resizedImages.Add(resizedImage);
					size += addedSize;
				}
				catch (Exception e)
				{
					var msg = "\r\n" + image.Name + " (Id: " + image.Id + ") - Failed \r\n";

					LogHelper.Error<ResizeMediaController>(msg, e);
				}
			}

			stopwatch.Stop();

			var retObj = new MediaResult
			{
				ResizedMedia = resizedImages,
				ElapsedTime = stopwatch.Elapsed.Seconds.ToString(),
				OldSize = oldSize,
				Size = ConvertBytesToMegabytes(size)
			};
			
			return Request.CreateResponse(HttpStatusCode.OK, retObj, new MediaTypeHeaderValue("application/json"));
		}

		[HttpGet]
		public HttpResponseMessage PreCheck()
		{
			var images = GetAllImages();
			var size = images.Select(x => x.GetValue<long>("umbracoBytes")).Sum();

			var retObj = new PreResizeResult
			{
				Count = images.Count,
				Size = ConvertBytesToMegabytes(size)
			};
			
			return Request.CreateResponse(HttpStatusCode.OK, retObj, new MediaTypeHeaderValue("application/json"));
		}

		[HttpGet]
		public HttpResponseMessage GetAll()
		{
			var images = GetAllImages();

			return Request.CreateResponse(HttpStatusCode.OK, images, new MediaTypeHeaderValue("application/json"));
		}

		private MediaResizeItem ResizeImage(IMedia image, string path, MediaFileSystem mediaFileSystem, int count, int imagesCount, out long size)
		{

			using (ImageFactory imageFactory = new ImageFactory(true))
			{
				var fullPath = mediaFileSystem.GetFullPath(path);

				ResizeLayer layer = new ResizeLayer(new Size(330, 0), ResizeMode.Max)
				{
					Upscale = false
				};

				var process = imageFactory.Load(fullPath)
					.Resize(layer)
					.Save(fullPath);

				var msg = "\r\n" + image.Name + " (Id: " + image.Id + ") - Succesfully resized \r\n" +
				          "Original size: " + image.GetValue<string>("umbracoWidth") + "px x " + image.GetValue<string>("umbracoHeight") + "px \r\n" +
				          "New size: " + process.Image.Width + "px x " + process.Image.Height + "px \r\n" +
				          "Count: " + count + " of " + imagesCount + "\r\n";

				LogHelper.Info<ResizeMediaController>(msg);

				ApplicationContext.Services.MediaService.Save(image);

				size = ApplicationContext.Services.MediaService.GetById(image.Id).GetValue<long>("umbracoBytes");

				return new MediaResizeItem
				{
					Id = image.Id,
					Name = image.Name,
					NewWidth = process.Image.Width + "px",
					NewHeight = process.Image.Height + "px",
					OldHeight = image.GetValue<string>("umbracoHeight") + "px",
					OldWidth = image.GetValue<string>("umbracoWidth") + "px"
				};
			}
		}

		private static double ConvertBytesToMegabytes(long bytes)
		{
			return (bytes / 1024f) / 1024f;
		}

		private List<IMedia> GetAllImages()
		{
			var images = new List<IMedia>();
			var allImages = ApplicationContext.Services.MediaService.GetRootMedia().ToList();
			images.AddRange(allImages.Where(x => x != null && x.HasProperty("umbracoFile")));

			var tt = allImages.SelectMany(x => x.Descendants().Where(y => x != null && y.HasProperty("umbracoFile")));
			images.AddRange(tt);

			return images;
		}
	}

	public class UmbracoPath
	{
		public string Src { get; set; }
		public string Crop { get; set; }
	}

	public class MediaResult
	{
		public double Size { get; set; }
		public double OldSize { get; set; }
		public string ElapsedTime { get; set; }
		public int Count => ResizedMedia.Count;
		public List<MediaResizeItem> ResizedMedia { get; set; }
	}

	public class PreResizeResult
	{
		public double Size { get; set; }
		public int Count { get; set; }
	}

	public class MediaResizeItem
	{
		public string Name { get; set; }
		public int Id { get; set; }
		public string NewWidth { get; set; }
		public string NewHeight{ get; set; }
		public string OldWidth { get; set; }
		public string OldHeight { get; set; }
	}
}