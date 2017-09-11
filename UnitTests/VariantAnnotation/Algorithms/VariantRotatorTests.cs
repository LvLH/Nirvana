﻿using Moq;
using UnitTests.TestDataStructures;
using VariantAnnotation.Algorithms;
using VariantAnnotation.Interface.AnnotatedPositions;
using VariantAnnotation.Interface.Positions;
using VariantAnnotation.Interface.Sequence;
using VariantAnnotation.Sequence;
using Xunit;

namespace UnitTests.VariantAnnotation.Algorithms
{
    public sealed class VariantRotatorTests
    {
        private static readonly IChromosome Chromosome = new Chromosome("chrBob", "bob", 2);

        private readonly ISequence _refSequence =
            new SimpleSequence(
                new string('A', VariantRotator.MaxDownstreamLength) + "ATGTGTGTGTGCAGT" +
                new string('A', VariantRotator.MaxDownstreamLength), 965891);

        [Fact]
        public void Right_Deletion()
        {
            // chr1	966391	.	ATG	A	2694.00	PASS	.
            var variant = GetDeletion();

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.Start).Returns(966300);
            transcript.SetupGet(x => x.End).Returns(966405);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(false);

            bool shiftToEnd;
            var rotatedVariant = VariantRotator.Right(variant.Object, transcript.Object, _refSequence,transcript.Object.Gene.OnReverseStrand,out shiftToEnd);

            Assert.NotNull(rotatedVariant);
            Assert.False(ReferenceEquals(variant.Object, rotatedVariant));
            Assert.Equal(966400, rotatedVariant.Start);
            Assert.Equal("TG", rotatedVariant.RefAllele);
            Assert.False(shiftToEnd);
        }

        [Fact]
        public void Right_Insertion()
        {
            var variant = GetInsertion();

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.Start).Returns(966300);
            transcript.SetupGet(x => x.End).Returns(966405);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(false);

            bool shiftToEnd;
            var rotatedVariant = VariantRotator.Right(variant.Object, transcript.Object, _refSequence, transcript.Object.Gene.OnReverseStrand, out shiftToEnd);

            Assert.NotNull(rotatedVariant);
            Assert.False(ReferenceEquals(variant.Object, rotatedVariant));
            Assert.Equal(966403, rotatedVariant.Start);
            Assert.Equal("TG", rotatedVariant.AltAllele);
            Assert.False(shiftToEnd);
        }

        [Fact]
        public void Right_Identity_WhenRefSequenceNull()
        {
            var originalVariant = GetDeletion();
            var rotatedVariant = VariantRotator.Right(originalVariant.Object, null, null, false, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        [Fact]
        public void Right_Identity_WhenNotInsertionOrDeletion()
        {
            var originalVariant = GetDeletion();
            originalVariant.SetupGet(x => x.Type).Returns(VariantType.SNV);
            var rotatedVariant = VariantRotator.Right(originalVariant.Object, null, _refSequence, false, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        [Fact]
        public void Right_Identity_VariantBeforeTranscript_ForwardStrand()
        {
            var originalVariant = GetDeletion();

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.Start).Returns(966397);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(false);

            var rotatedVariant = VariantRotator.Right(originalVariant.Object, transcript.Object, _refSequence, transcript.Object.Gene.OnReverseStrand, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        [Fact]
        public void Right_Identity_VariantBeforeTranscript_ReverseStrand()
        {
            var originalVariant = GetDeletion();

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.End).Returns(966390);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(true);

            var rotatedVariant = VariantRotator.Right(originalVariant.Object, transcript.Object, _refSequence, transcript.Object.Gene.OnReverseStrand, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        [Fact]
        public void Right_Identity_InsertionVariantBeforeTranscript_ForwardStrand()
        {
            var originalVariant = GetInsertion();

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.End).Returns(966392);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(false);

            var rotatedVariant = VariantRotator.Right(originalVariant.Object, transcript.Object, _refSequence, transcript.Object.Gene.OnReverseStrand, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        [Fact]
        public void Right_Identity_WithNoRotation()
        {
            var originalVariant = GetDeletion();

            ISequence refSequence = new SimpleSequence(
                new string('A', VariantRotator.MaxDownstreamLength) + "GAGAGTTAGGTA" +
                new string('A', VariantRotator.MaxDownstreamLength), 965891);

            var transcript = new Mock<ITranscript>();
            transcript.SetupGet(x => x.Start).Returns(966300);
            transcript.SetupGet(x => x.End).Returns(966405);
            transcript.SetupGet(x => x.Gene.OnReverseStrand).Returns(false);

            var rotatedVariant = VariantRotator.Right(originalVariant.Object, transcript.Object, refSequence, transcript.Object.Gene.OnReverseStrand, out _);
            Assert.True(ReferenceEquals(originalVariant.Object, rotatedVariant));
        }

        private static Mock<ISimpleVariant> GetDeletion()
        {
            var variant = new Mock<ISimpleVariant>();
            variant.SetupGet(x => x.Chromosome).Returns(Chromosome);
            variant.SetupGet(x => x.Start).Returns(966392);
            variant.SetupGet(x => x.End).Returns(966394);
            variant.SetupGet(x => x.RefAllele).Returns("TG");
            variant.SetupGet(x => x.AltAllele).Returns("");
            variant.SetupGet(x => x.Type).Returns(VariantType.deletion);
            return variant;
        }

        private static Mock<ISimpleVariant> GetInsertion()
        {            
            var variant = new Mock<ISimpleVariant>();
            variant.SetupGet(x => x.Chromosome).Returns(Chromosome);
            variant.SetupGet(x => x.Type).Returns(VariantType.insertion);
            variant.SetupGet(x => x.Start).Returns(966397);
            variant.SetupGet(x => x.End).Returns(966396);
            variant.SetupGet(x => x.RefAllele).Returns("");
            variant.SetupGet(x => x.AltAllele).Returns("TG");
            return variant;
        }
    }
}