﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VariantAnnotation.Interface.IO;
using VariantAnnotation.Interface.Positions;
using VariantAnnotation.Interface.Providers;
using VariantAnnotation.Interface.Sequence;

namespace Vcf.VariantCreator
{
    public sealed class VariantFactory
    {
        private readonly IRefMinorProvider _refMinorProvider;
        private readonly IDictionary<string, IChromosome> _refNameToChromosome;
        private const string StrPrefix = "<STR";
        private const string CnvPrefix = "<CN";
        private readonly bool _enableVerboseTranscript;

        public VariantFactory(IDictionary<string, IChromosome> refNameToChromosome, IRefMinorProvider refMinorProvider,
            bool enableVerboseTranscript)
        {
            _refNameToChromosome     = refNameToChromosome;
            _refMinorProvider        = refMinorProvider;
            _enableVerboseTranscript = enableVerboseTranscript;
        }

        private static VariantCategory GetVariantCategory(string[] altAlleles, bool isReference, bool isSymbolicAllele)
        {
            if (isReference) return VariantCategory.Reference;
            if (IsBreakend(altAlleles)) return VariantCategory.SV;
            if (!isSymbolicAllele) return VariantCategory.SmallVariant;
            if (altAlleles.Any(x => x.StartsWith(StrPrefix))) return VariantCategory.RepeatExpansion;
            if (altAlleles.Any(x => x.StartsWith(CnvPrefix))) return VariantCategory.CNV;
            return VariantCategory.SV;
        }

        private static bool IsBreakend(string[] altAlleles) =>
            altAlleles.Any(x => x.Contains("[")) || altAlleles.Any(x => x.Contains("]"));

        private static bool IsSymbolicAllele(string altAllele) =>
            altAllele.StartsWith("<") && altAllele.EndsWith(">") &&
            !VcfCommon.NonInformativeAltAllele.Contains(altAllele);

        public IVariant[] CreateVariants(IChromosome chromosome, string id, int start, int end, string refAllele,
            string[] altAlleles, IInfoData infoData, int? sampleCopyNumber)
        {
            var isReference      = altAlleles.Length == 1 && (altAlleles[0] == "." || altAlleles[0] == VcfCommon.GatkNonRefAllele);
            var isSymbolicAllele = altAlleles.Any(IsSymbolicAllele);
            var variantCategory  = GetVariantCategory(altAlleles, isReference, isSymbolicAllele);

            if (isReference) return new[] { GetVariant(chromosome, id, start, end, refAllele, altAlleles[0], infoData, variantCategory, sampleCopyNumber) };

            var variants = new List<IVariant>();
            foreach (string altAllele in altAlleles)
            {
                if (VcfCommon.NonInformativeAltAllele.Contains(altAllele)) continue;
                variants.Add(GetVariant(chromosome, id, start, end, refAllele, altAllele, infoData, variantCategory, sampleCopyNumber));
            }

            return variants.Count == 0 ? null : variants.ToArray();
        }

        private IVariant GetVariant(IChromosome chromosome, string id, int start, int end, string refAllele, string altAllele, IInfoData infoData, VariantCategory category, int? sampleCopyNumber)
        {
            switch (category)
            {
                case VariantCategory.Reference:
                    var refMinorGlobalMajorAllele = _refMinorProvider?.GetGlobalMajorAlleleForRefMinor(chromosome, start);
                    return ReferenceVariantCreator.Create(chromosome, start, end, refAllele, altAllele, refMinorGlobalMajorAllele);
                case VariantCategory.SmallVariant:
                    return SmallVariantCreator.Create(chromosome, start, refAllele, altAllele);

                case VariantCategory.SV:
                    var svBreakEnds = infoData.SvType == VariantType.translocation_breakend ?
                        GetTranslocationBreakends(chromosome, refAllele, altAllele, start) :
                        GetSvBreakEnds(chromosome.EnsemblName, start, infoData.SvType, infoData.End, infoData.IsInv3, infoData.IsInv5);
                    return StructuralVariantCreator.Create(chromosome, start, refAllele, altAllele, svBreakEnds, infoData, _enableVerboseTranscript);

                case VariantCategory.CNV:
                    return CnvCreator.Create(chromosome, id, start, refAllele, infoData, sampleCopyNumber, _enableVerboseTranscript);
                case VariantCategory.RepeatExpansion:
                    return RepeatExpansionCreator.Create(chromosome, start, refAllele, altAllele, infoData);
                default:
                    throw new NotImplementedException("Unrecognized variant category.");
            }
        }

        internal IBreakEnd[] GetTranslocationBreakends(IChromosome chromosome1, string refAllele, string altAllele, int position1)
        {
            var breakendInfo = ParseBreakendAltAllele(refAllele, altAllele);

            var chromosome2 = breakendInfo.Chromosome2;
            var position2   = breakendInfo.Position2;
            var isSuffix1   = breakendInfo.IsSuffix1;
            var isSuffix2   = breakendInfo.IsSuffix2;

            return new IBreakEnd[] { new BreakEnd(chromosome1, chromosome2, position1, position2, isSuffix1, isSuffix2) };
        }

        internal IBreakEnd[] GetSvBreakEnds(string ensemblName, int start, VariantType svType, int? svEnd, bool isInv3, bool isInv5)
        {
            if (svEnd == null) return null;

            var end        = svEnd.Value;
            var breakEnds  = new IBreakEnd[2];
            var chromosome = GetChromosome(ensemblName);

            switch (svType)
            {
                case VariantType.deletion:
                    breakEnds[0] = new BreakEnd(chromosome, chromosome, start, end + 1, false, true);
                    breakEnds[1] = new BreakEnd(chromosome, chromosome, end + 1, start, true, false);
                    break;

                case VariantType.tandem_duplication:
                case VariantType.duplication:
                    breakEnds[0] = new BreakEnd(chromosome, chromosome, end, start, false, true);
                    breakEnds[1] = new BreakEnd(chromosome, chromosome, start, end, true, false);
                    break;
                case VariantType.inversion:
                    if (isInv3)
                    {
                        breakEnds[0] = new BreakEnd(chromosome, chromosome, start, end, false, false);
                        breakEnds[1] = new BreakEnd(chromosome, chromosome, end, start, false, false);
                        break;
                    }
                    if (isInv5)
                    {
                        breakEnds[0] = new BreakEnd(chromosome, chromosome, start + 1, end + 1, true, true);
                        breakEnds[1] = new BreakEnd(chromosome, chromosome, end + 1, start + 1, true, true);
                        break;
                    }

                    breakEnds[0] = new BreakEnd(chromosome, chromosome, start, end, false, false);
                    breakEnds[1] = new BreakEnd(chromosome, chromosome, end + 1, start + 1, true, true);
                    break;
                default:
                    return null;
            }

            return breakEnds;
        }

        private IChromosome GetChromosome(string chrom) => _refNameToChromosome.ContainsKey(chrom)
            ? _refNameToChromosome[chrom]
            : new EmptyChromosome(chrom);

        private const string ForwardBreakEnd = "[";

        /// <summary>
        /// parses the alternate allele
        /// </summary>
        private (IChromosome Chromosome2, int Position2, bool IsSuffix1, bool IsSuffix2) ParseBreakendAltAllele(
            string refAllele, string altAllele)
        {
            string referenceName2;
            int position2;
            bool isSuffix2;

            // (\w+)([\[\]])([^:]+):(\d+)([\[\]])
            // ([\[\]])([^:]+):(\d+)([\[\]])(\w+)
            if (altAllele.StartsWith(refAllele))
            {
                var forwardRegex = new Regex(@"\w+([\[\]])([^:]+):(\d+)([\[\]])", RegexOptions.Compiled);
                var match = forwardRegex.Match(altAllele);

                if (!match.Success)
                    throw new InvalidDataException(
                        "Unable to successfully parse the complex rearrangements for the following allele: " + altAllele);

                isSuffix2      = match.Groups[4].Value == ForwardBreakEnd;
                position2      = Convert.ToInt32(match.Groups[3].Value);
                referenceName2 = match.Groups[2].Value;

                return (GetChromosome(referenceName2), position2, false, isSuffix2);
            }
            else
            {
                var reverseRegex = new Regex(@"([\[\]])([^:]+):(\d+)([\[\]])\w+", RegexOptions.Compiled);
                var match = reverseRegex.Match(altAllele);

                if (!match.Success)
                    throw new InvalidDataException(
                        "Unable to successfully parse the complex rearrangements for the following allele: " + altAllele);

                isSuffix2      = match.Groups[1].Value == ForwardBreakEnd;
                position2      = Convert.ToInt32(match.Groups[3].Value);
                referenceName2 = match.Groups[2].Value;

                return (GetChromosome(referenceName2), position2, true, isSuffix2);
            }
        }

        private enum VariantCategory
        {
            Reference,
            SmallVariant,
            SV,
            CNV,
            RepeatExpansion
        }
    }
}