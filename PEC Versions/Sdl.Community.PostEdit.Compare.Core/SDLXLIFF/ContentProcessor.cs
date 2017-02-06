﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace Sdl.Community.PostEdit.Compare.Core.SDLXLIFF
{
    public class ContentProcessor : IBilingualContentProcessor
    {



        internal bool IncludeTagText = true;
        internal Dictionary<string, Dictionary<string, ParagraphUnit>> FileParagraphUnits { get; set; }


        public CultureInfo SourceLanguageCultureInfo { get; set; }
        public CultureInfo TargetLanguageCultureInfo { get; set; }

        public string SourceLanguageId { get; set; }
        public string TargetLanguageId { get; set; }

        public string TmPath { get; set; }
        private IFileProperties CurrentFileProperties { get; set; }
        private Dictionary<string, ParagraphUnit> ParagraphUnits { get; set; }


        internal string OutputPath = string.Empty;
        internal bool CreatedDummyOutput = false;
        internal List<string> DummyOutputFiles = new List<string>();

        #region  |  Content Generator  |
        private ContentGenerator _contentGeneratorProcessor;

        public ContentGenerator ContentGeneratorProcessor
        {
            get { return _contentGeneratorProcessor ?? (_contentGeneratorProcessor = new ContentGenerator()); }
        }
        #endregion

        #region  |  IBilingualContentProcessor Members  |


        public ContentProcessor()
        {

            FileParagraphUnits = new Dictionary<string, Dictionary<string, ParagraphUnit>>();
        }


        public IBilingualContentHandler Output
        {
            get;
            set;
        }

        #endregion

        #region  |  IBilingualContentHandler Members  |




        public void Complete()
        {
            //not needed for this implementation
        }

        public void FileComplete()
        {
            if (FileParagraphUnits.ContainsKey(CurrentFileProperties.FileConversionProperties.OriginalFilePath))
            {
                var paragraphUnits = FileParagraphUnits[CurrentFileProperties.FileConversionProperties.OriginalFilePath];

                foreach (var kvp in ParagraphUnits)
                {
                    if (!paragraphUnits.ContainsKey(kvp.Key))
                    {
                        paragraphUnits.Add(kvp.Key, kvp.Value);
                    }
                }
                FileParagraphUnits[CurrentFileProperties.FileConversionProperties.OriginalFilePath] = paragraphUnits;
            }
            else
            {
                FileParagraphUnits.Add(CurrentFileProperties.FileConversionProperties.OriginalFilePath, ParagraphUnits);
            }
        }

        public void Initialize(IDocumentProperties documentInfo)
        {
            SourceLanguageId = string.Empty;
            TargetLanguageId = string.Empty;

            if (documentInfo.SourceLanguage != null
                && documentInfo.SourceLanguage.CultureInfo != null)
                SourceLanguageId = documentInfo.SourceLanguage.CultureInfo.Name;
            if (documentInfo.TargetLanguage != null
                && documentInfo.TargetLanguage.CultureInfo != null)
                TargetLanguageId = documentInfo.TargetLanguage.CultureInfo.Name;


            var st = new Settings();
            var tmName = "TM_" + SourceLanguageId + ".sdltm";
            var tmFullPath = Path.Combine(st.ApplicationSettingsPathTm, tmName);
            TM.Tm.SetTm(tmFullPath, SourceLanguageId, "it-IT");

        }

        public void ProcessParagraphUnit(IParagraphUnit paragraphUnit)
        {
            #region  |  for comments that were added at the paragraph level  |

            var paragraphComments = new List<Comment>();

            if (paragraphUnit.Properties.Comments != null && paragraphUnit.Properties.Comments.Count > 0)
            {
                foreach (var commmentItem in paragraphUnit.Properties.Comments.Comments)
                {
                    var comment = commmentItem;
                    var commentNew = new Comment { Author = comment.Author };
                    if (comment.DateSpecified)
                        commentNew.Date = comment.Date;
                    commentNew.Severity = comment.Severity.ToString();
                    commentNew.Text = comment.Text;
                    commentNew.Version = comment.Version;

                    paragraphComments.Add(commentNew);
                }
            }
            #endregion



            var index = -1;
            foreach (var segmentPair in paragraphUnit.SegmentPairs)
            {
                index++;

                #region  |  initialize and assign values to xSegmentPair  |

                var pair = new SegmentPair();

                ContentGeneratorProcessor.ProcessSegment(segmentPair.Source, IncludeTagText);

                ContentGeneratorProcessor.Segment.Culture = new CultureInfo(SourceLanguageId);
                
                var words = 0;
                var chars = 0;
                var tags = 0;
                var placeholders = 0;
                //var results = TM.Tm.GetSearchResults(ContentGeneratorProcessor.Segment);
                //if (results != null)
                //{
                //    words = results.SourceWordCounts.Words;
                //    chars = results.SourceWordCounts.Characters;
                //    tags = results.SourceWordCounts.Tags;
                //    placeholders = results.SourceWordCounts.Placeables;
                //}
                Comparison.Comparer.GetStatisticalContentCounts(ContentGeneratorProcessor.SegmentSections, ref words, ref chars, ref tags, ref placeholders);

                pair.SourceWords = words;
                pair.SourceChars = chars;
                pair.SourceTags = tags;
                pair.SourcePlaceables = placeholders;

                pair.Source = ContentGeneratorProcessor.PlainText.ToString();
                pair.SourceSections = ContentGeneratorProcessor.SegmentSections;

                ContentGeneratorProcessor.ProcessSegment(segmentPair.Target, IncludeTagText);
                pair.Target = ContentGeneratorProcessor.PlainText.ToString();
                pair.TargetSections = ContentGeneratorProcessor.SegmentSections;
                pair.Id = segmentPair.Properties.Id.Id;
                pair.SegmentStatus = segmentPair.Properties.ConfirmationLevel.ToString();
                pair.IsLocked = segmentPair.Properties.IsLocked;
                pair.Comments = ContentGeneratorProcessor.Comments;

                #region  |  for comments that were added at the paragraph level  |

                if (paragraphComments.Count > 0 && index == 0)
                {
                    if (pair.Comments.Count > 0)
                    {
                        foreach (var commentParagraph in paragraphComments)
                        {
                            var foundComment = pair.Comments.Any(commentSegmentPair => string.CompareOrdinal(commentParagraph.Text.Trim(), commentSegmentPair.Text.Trim()) == 0);


                            if (!foundComment)
                            {
                                pair.Comments.Add(commentParagraph);
                            }
                        }
                    }
                    else
                    {
                        foreach (var comment in paragraphComments)
                        {
                            pair.Comments.Add(comment);
                        }
                    }
                }
                #endregion


                if (segmentPair.Properties.TranslationOrigin != null)
                {
                    pair.TranslationOrigin.IsRepeated = segmentPair.Properties.TranslationOrigin.IsRepeated;
                    pair.TranslationOrigin.IsStructureContextMatch = segmentPair.Properties.TranslationOrigin.IsStructureContextMatch;
                    pair.TranslationOrigin.MatchPercentage = segmentPair.Properties.TranslationOrigin.MatchPercent;
                    pair.TranslationOrigin.OriginSystem = segmentPair.Properties.TranslationOrigin.OriginSystem;
                    pair.TranslationOrigin.OriginType = segmentPair.Properties.TranslationOrigin.OriginType;
                    pair.TranslationOrigin.RepetitionTableId = segmentPair.Properties.TranslationOrigin.RepetitionTableId.Id;
                    pair.TranslationOrigin.TextContextMatchLevel = segmentPair.Properties.TranslationOrigin.TextContextMatchLevel.ToString();
                }

                #endregion

                #region  |  add the SegmentPair to the xParagraphs dictionary  |

                if (ParagraphUnits.ContainsKey(paragraphUnit.Properties.ParagraphUnitId.Id))
                {
                    var unit = ParagraphUnits[paragraphUnit.Properties.ParagraphUnitId.Id];
                    unit.SegmentPairs.Add(pair);
                    ParagraphUnits[paragraphUnit.Properties.ParagraphUnitId.Id] = unit;
                }
                else
                {
                    var segmentPairs = new List<SegmentPair> { pair };
                    ParagraphUnits.Add(paragraphUnit.Properties.ParagraphUnitId.Id, new ParagraphUnit(paragraphUnit.Properties.ParagraphUnitId.Id, segmentPairs));
                }
                #endregion
            }
        }


        public void SetFileProperties(IFileProperties fileInfo)
        {
            CurrentFileProperties = fileInfo;
            ParagraphUnits = new Dictionary<string, ParagraphUnit>();
        }

        #endregion


    }
}
