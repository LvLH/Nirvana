﻿using System.IO;
using System.Linq;
using SAUtils.Revel;
using UnitTests.TestUtilities;
using Xunit;

namespace UnitTests.SAUtils.Revel
{
    public sealed class RevelReaderTests
    {
        private static Stream GetStream()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.WriteLine("chr,hg19_pos,ref,alt,aaref,aaalt,REVEL");
            writer.WriteLine("1,35290,G,A,P,D,0.035");
            writer.WriteLine("1,35290,G,A,P,S,0.031");
            writer.WriteLine("1,35290,G,C,P,A,0.040");
            writer.WriteLine("1,35290,G,T,P,T,0.035");
            writer.WriteLine("1,35290,G,C,P,A,0.063");
            writer.WriteLine("1,35291,G,C,F,L,0.022");
            writer.WriteLine("1,35291,G,T,F,L,0.022");
            writer.Flush();

            stream.Position = 0;
            return stream;
        }

        [Fact]
        public void RevelReader_GetItems_AsExpected()
        {
            using (var streamReader = new StreamReader(GetStream()))
            using (var reader = new RevelReader(streamReader, ChromosomeUtilities.RefNameToChromosome))
            {
                var revelItems = reader.GetItems().ToArray();
                Assert.Equal(5, revelItems.Length);
                Assert.Equal(35290, revelItems[0].Position);
                Assert.Equal("G", revelItems[0].RefAllele);
                Assert.Equal("A", revelItems[0].AltAllele);
                Assert.Equal("\"score\":0.035", revelItems[0].GetJsonString());
                Assert.Equal(35290, revelItems[1].Position);
                Assert.Equal("G", revelItems[1].RefAllele);
                Assert.Equal("C", revelItems[1].AltAllele);
                Assert.Equal("\"score\":0.063", revelItems[1].GetJsonString());
                Assert.Equal(35291, revelItems[4].Position);
                Assert.Equal("G", revelItems[4].RefAllele);
                Assert.Equal("T", revelItems[4].AltAllele);
                Assert.Equal("\"score\":0.022", revelItems[4].GetJsonString());
            }
        }
    }
}