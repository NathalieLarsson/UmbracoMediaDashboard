using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using ImageProcessor;
using ImageProcessor.Imaging;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Events;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Umbraco.Media.Business
{
	public class MediaUpload : IApplicationEventHandler
	{
		public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
		{
		}

		public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
		{
			//MediaService.Saving += SavingMedia;
		}

		public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
		{
		}

		private void SavingMedia(IMediaService sender, SaveEventArgs<IMedia> e)
		{
			MediaFileSystem mediaFileSystem = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();
			IContentSection contentSection = UmbracoConfig.For.UmbracoSettings().Content;
			IEnumerable<string> supportedTypes = contentSection.ImageFileTypes.ToList();

			foreach (var entity in e.SavedEntities)
			{
				if (!entity.HasProperty("umbracoFile")) continue;

				var path = entity.GetValue<string>("umbracoFile");
				var extension = Path.GetExtension(path)?.Substring(1);

				if (!supportedTypes.InvariantContains(extension)) continue;

				var fullPath = mediaFileSystem.GetFullPath(path);
				using (ImageFactory imageFactory = new ImageFactory(true))
				{
					ResizeLayer layer = new ResizeLayer(new Size(1920, 0), ResizeMode.Max)
					{
						Upscale = false
					};

					imageFactory.Load(fullPath)
						.Resize(layer)
						.Save(fullPath);
				}
			}
		}
	}
}