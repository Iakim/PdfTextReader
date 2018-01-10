﻿using PdfTextReader.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfTextReader.PDFText;

namespace PdfTextReader.PDFCore
{
    class RemoveOverlapedImages : IProcessBlock, IValidateBlock, IPipelineDependency
    {
        private List<IBlock> _images;
        private PreProcessImages _parse;

        public void SetPage(PipelinePage p)
        {
            var parseImage = p.CreateInstance<PreProcessImages>();

            var page = parseImage.Images;

            if (page == null)
                throw new InvalidOperationException("RemoveHeaderImage requires PreProcessImages");

            this._images = page.AllBlocks.ToList();

            this._parse = parseImage;
        }

        public BlockPage Process(BlockPage page)
        {
            var overlappedImages = FindInlineElements(page);

            foreach(var image in overlappedImages.AllBlocks)
            {
                _parse.RemoveImage(image);
            }

            var blocks = page.AllBlocks.Except(overlappedImages.AllBlocks);

            var result = new BlockPage();

            result.AddRange(blocks);

            return result;
        }

        public BlockPage Validate(BlockPage page)
        {
            return FindInlineElements(page);
        }

        BlockPage FindInlineElements(BlockPage page)
        {
            var blocks = page.AllBlocks.ToList();
            var overlapped = new bool[blocks.Count];
            var result = new BlockPage();

            for(int i=0; i<blocks.Count; i++)
            {
                for(int j=i+1; j<blocks.Count; j++)
                {
                    if(Block.HasOverlap(blocks[i], blocks[j]))
                    {
                        overlapped[j] = true;
                    }
                }
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                if(overlapped[i] == true)
                {
                    result.Add(blocks[i]);
                }
            }

            return result;
        }        
    }
}
