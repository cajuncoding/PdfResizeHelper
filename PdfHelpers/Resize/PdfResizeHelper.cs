﻿using iTextSharp.awt.geom;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.IO;

namespace PdfHelpers.Resize
{
    public static class PdfResizeHelper
    {
        public static byte[] ResizePdfPageSize(byte[] pdfBytes, PdfResizeInfo targetSizeInfo, PdfScalingOptions scalingOptions = null)
        {
            if(pdfBytes == null) throw new ArgumentNullException(nameof(pdfBytes), "Pdf Byte Array cannot be null.");
            if (targetSizeInfo == null) throw new ArgumentNullException(nameof(targetSizeInfo), "ResizeInfo cannot be null.");

            //Initialize with Default Scaling Options...
            var pdfScalingOptions = scalingOptions ?? PdfScalingOptions.Default;

            //BBernard
            //Statically ensure that Compression is enabled...
            Document.Compress = true;

            var marginInfo = targetSizeInfo.MarginSize;

            using (var outputMemoryStream = new MemoryStream())
            using (var pdfDocBuilder = new Document(targetSizeInfo.PageSize, marginInfo.Left, marginInfo.Right, marginInfo.Top, marginInfo.Bottom))
            using (var pdfReader = new PdfReader(pdfBytes))
            using (var pdfWriter = PdfWriter.GetInstance(pdfDocBuilder, outputMemoryStream))
            {
                pdfDocBuilder.Open();

                var pageCount = pdfReader.NumberOfPages;
                for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
                {
                    //Read the content for the current page...
                    //NOTE: We use the PdfWriter to import the Page (not the Document) to ensures that all required
                    //      references (e.g. Fonts, Symbols, Images, etc.) are all imported into the target Doc builder.
                    PdfImportedPage page = pdfWriter.GetImportedPage(pdfReader, pageNumber);

                    //Scale the content for the target parameters...
                    var scaledTemplateInfo = ScalePdfContentForTargetDoc(page, pdfDocBuilder, pdfScalingOptions);

                    //Set the Page dimensions processed by the Scaling logic (e.g. Supports dynamic use of Landscape Orientation)...
                    //  and then move the doc cursor to initialize a new page with these settings so we can add the content.
                    pdfDocBuilder.SetPageSize(scaledTemplateInfo.ScaledPageSize);
                    pdfDocBuilder.NewPage();

                    //Add the scaled content to the Document (ie. Pdf Template with the Content Embedded)...
                    pdfDocBuilder.Add(scaledTemplateInfo.ScaledPdfContent);

                    //Now we need to Reset the page size to the target size for processing of the next page...
                    //NOTE: the Margins never change so we don't need to re-set them.
                    pdfDocBuilder.SetPageSize(targetSizeInfo.PageSize);
                }

                pdfDocBuilder.Close();

                byte[] finalFileBytes = outputMemoryStream.ToArray();
                return finalFileBytes;
            }
        }

        public static PdfScaledTemplateInfo ScalePdfContentForTargetDoc(PdfImportedPage currentPage, Document targetDoc, PdfScalingOptions scalingOptions = null)
        {
            return ScalePdfContentForTargetDoc(new ImgTemplate(currentPage), targetDoc, scalingOptions);
        }

        public static PdfScaledTemplateInfo ScalePdfContentForTargetDoc(ImgTemplate contentTemplate, Document targetDoc, PdfScalingOptions scalingOptions = null)
        {
            if (contentTemplate == null) throw new ArgumentNullException(nameof(contentTemplate), "Pdf Content to be resized cannot be null.");
            if (targetDoc == null) throw new ArgumentNullException(nameof(targetDoc), "Target Pdf Document builder cannot be null.");

            var pdfContent = new ImgTemplate(contentTemplate);

            //Initialize with Default Scaling Options...
            var pdfScalingOptions = scalingOptions ?? PdfScalingOptions.Default;

            //Don't mutate the original value...
            //var targetSize = targetDoc.PageSize;
            var targetSize = targetDoc.PageSize;
            var targetWidth = targetSize.Width - targetDoc.LeftMargin - targetDoc.RightMargin;
            var targetHeight = targetSize.Height - targetDoc.TopMargin - targetDoc.BottomMargin;
            var pageOrientation = PdfPageOrientation.Portrait;

            //Determine if we should force Resize into the specified Target Size or if we should enable Landscape orientation 
            //  (e.g. 90 degree rotation) to accomodate pages that are wider than taller...
            //NOTE: If landscape orientation is enabled then we only need to make adjustments if all of the following are true:
            //      a) the current size is using landscape orientation
            //      b) and the target size is not already in the same orientation (e.g. landscape)
            if (pdfScalingOptions.EnableDynamicLandscapeOrientation
                && pdfContent.Width > pdfContent.Height 
                && targetHeight > targetWidth)
            {
                targetSize = targetDoc.PageSize.Rotate();
                //Don't mutate the original value...
                //var targetSize = targetDoc.PageSize;
                targetWidth = targetSize.Width - targetDoc.LeftMargin - targetDoc.RightMargin;
                targetHeight = targetSize.Height - targetDoc.TopMargin - targetDoc.BottomMargin;

                pageOrientation = PdfPageOrientation.Landscape;
            }

            bool scalingEnabled = false;
            //Flag to denote which dimension is the constraining one
            //NOTE: This changes based on if we are scaling the size Up or Down!
            switch (pdfScalingOptions.PdfContentScalingMode)
            {
                case PdfResizeScalingMode.ScaleAlways:
                    scalingEnabled = true;
                    break;
                case PdfResizeScalingMode.ScaleDownOnly:
                    scalingEnabled = pdfContent.Width > targetWidth || pdfContent.Height > targetHeight;
                    break;
                case PdfResizeScalingMode.ScaleUpOnly:
                    scalingEnabled = pdfContent.Width < targetWidth || pdfContent.Height < targetHeight;
                    break;
            }

            //Support Maintaining Aspect Ratio...
            if (scalingEnabled && pdfScalingOptions.MaintainAspectRatio)
            {
                pdfContent.ScaleToFit(targetWidth, targetHeight);
            }
            //Support Skewed Resizing...
            else if (scalingEnabled)
            {
                pdfContent.ScaleAbsolute(targetWidth, targetHeight);
            }
            //Do nothing if scaling is not enabled due to parameters and current size details...
            //else { }

            //If Enabled then we adjust the position to center the content on the Page...
            if (pdfScalingOptions.EnableContentCentering)
            {
                var x = (targetWidth - pdfContent.ScaledWidth) / 2;
                var y = (targetHeight - pdfContent.ScaledHeight) / 2;
                pdfContent.SetAbsolutePosition(x, y);
            }

            return new PdfScaledTemplateInfo()
            {
                ScaledPdfContent = pdfContent,
                ScaledPageOrientation = pageOrientation,
                ScaledPageSize = targetSize
            };
        }

        public static Point ComputeCenteredLocationPoint(Rectangle currentSize, Rectangle targetPageSize)
        {
            var x = (targetPageSize.Width - currentSize.Width) / 2;
            var y = (targetPageSize.Height - currentSize.Height) / 2;
            var centeredPosition = new Point(x, y);
            return centeredPosition;
        }

    }
}