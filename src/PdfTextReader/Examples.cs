﻿using PdfTextReader.Base;
using PdfTextReader.Execution;
using PdfTextReader.Parser;
using PdfTextReader.PDFCore;
using PdfTextReader.PDFText;
using PdfTextReader.TextStructures;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace PdfTextReader
{
    class Examples
    {
        public static PipelineText<TextStructure> GetTextParagraphs(string basename)
        {
            var pipeline = new Execution.Pipeline();

            var result =
            pipeline.Input($"bin/{basename}.pdf")
                    .Output($"bin/{basename}-test-output.pdf")
                    .AllPages<CreateStructures>(ProcessPage)
                    .ConvertText<CreateParagraphs, TextStructure>();

            return result;
        }

        public static void ProcessPage(PipelineInputPdf.PipelineInputPdfPage page)
        {
            page.ParsePdf<PreProcessTables>()
                        .ParseBlock<IdentifyTables>()
                    .ParsePdf<PreProcessImages>()
                            .Validate<RemoveOverlapedImages>().ShowErrors(p => p.Show(Color.Red))
                        .ParseBlock<RemoveOverlapedImages>()
                    .ParsePdf<ProcessPdfText>()
                        //.Validate<MergeTableText>().ShowErrors(p => p.Show(Color.Blue))
                        .ParseBlock<MergeTableText>()
                        //.Validate<HighlightTextTable>().ShowErrors(p => p.Show(Color.Green))
                        .ParseBlock<HighlightTextTable>()
                        .ParseBlock<RemoveTableText>()
                        .ParseBlock<GroupLines>()
                            .Show(Color.Orange)
                            .Validate<RemoveHeaderImage>().ShowErrors(p => p.Show(Color.Purple))
                            .ParseBlock<RemoveHeaderImage>()
                        .ParseBlock<FindInitialBlocksetWithRewind>()
                        .ParseBlock<BreakColumns>()
                            .Validate<RemoveFooter>().ShowErrors(p => p.Show(Color.Purple))
                            .ParseBlock<RemoveFooter>()
                        .ParseBlock<AddTableSpace>()
                        .ParseBlock<AddImageSpace>()
                        .ParseBlock<BreakInlineElements>()
                        .ParseBlock<ResizeBlocksets>()
                            .Validate<ResizeBlocksets>().ShowErrors(p => p.Show(Color.Red))
                        .ParseBlock<OrderBlocksets>()
                        .Show(Color.Orange)
                        .ShowLine(Color.Black);
        }
    }
}
