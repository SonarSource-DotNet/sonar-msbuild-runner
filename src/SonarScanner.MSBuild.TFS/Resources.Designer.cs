﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarScanner.MSBuild.TFS {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarScanner.MSBuild.TFS.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Found corresponding Binary-to-XML conversion output file for {0}, no conversion will be attempted..
        /// </summary>
        internal static string COVXML_DIAG_FileAlreadyExist_NoConversionAttempted {
            get {
                return ResourceManager.GetString("COVXML_DIAG_FileAlreadyExist_NoConversionAttempted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;sonar.cs.vscoveragexml.reportsPaths&apos; provided, skipping the search for coveragexml file in default folders....
        /// </summary>
        internal static string COVXML_DIAG_SkippingCoverageCheckPropertyProvided {
            get {
                return ResourceManager.GetString("COVXML_DIAG_SkippingCoverageCheckPropertyProvided", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The Coverage Report Processor was not initialized before use..
        /// </summary>
        internal static string EX_CoverageReportProcessorNotInitialised {
            get {
                return ResourceManager.GetString("EX_CoverageReportProcessorNotInitialised", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fetching code coverage report information from TFS....
        /// </summary>
        internal static string PROC_DIAG_FetchingCoverageReportInfoFromServer {
            get {
                return ResourceManager.GetString("PROC_DIAG_FetchingCoverageReportInfoFromServer", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis failed for SonarQube project {0}.
        /// </summary>
        internal static string Report_AnalysisFailed {
            get {
                return ResourceManager.GetString("Report_AnalysisFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Analysis succeeded for SonarQube project {0} [(Analysis results)] ({1}).
        /// </summary>
        internal static string Report_AnalysisSucceeded {
            get {
                return ResourceManager.GetString("Report_AnalysisSucceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to   Invalid projects: {0}, skipped projects: {1}, excluded projects: {2}.
        /// </summary>
        internal static string Report_InvalidSkippedAndExcludedMessage {
            get {
                return ResourceManager.GetString("Report_InvalidSkippedAndExcludedMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to   Product projects: {0}, test projects: {1}.
        /// </summary>
        internal static string Report_ProductAndTestMessage {
            get {
                return ResourceManager.GetString("Report_ProductAndTestMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;{0}&quot;, version {2}.
        /// </summary>
        internal static string Report_SonarQubeProjectDescription {
            get {
                return ResourceManager.GetString("Report_SonarQubeProjectDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Updating the TeamBuild summary....
        /// </summary>
        internal static string Report_UpdatingTeamBuildSummary {
            get {
                return ResourceManager.GetString("Report_UpdatingTeamBuildSummary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Absolute path to coverage file: {0}.
        /// </summary>
        internal static string TRX_DIAG_AbsoluteTrxPath {
            get {
                return ResourceManager.GetString("TRX_DIAG_AbsoluteTrxPath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following code coverage attachments were found from the trx files: {0}.
        /// </summary>
        internal static string TRX_DIAG_CodeCoverageAttachmentsFound {
            get {
                return ResourceManager.GetString("TRX_DIAG_CodeCoverageAttachmentsFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Looking for TRX files in: {0}.
        /// </summary>
        internal static string TRX_DIAG_FolderPaths {
            get {
                return ResourceManager.GetString("TRX_DIAG_FolderPaths", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to locate a test results (.trx) file....
        /// </summary>
        internal static string TRX_DIAG_LocatingTrx {
            get {
                return ResourceManager.GetString("TRX_DIAG_LocatingTrx", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No code coverage attachments were found from the trx files..
        /// </summary>
        internal static string TRX_DIAG_NoCodeCoverageInfo {
            get {
                return ResourceManager.GetString("TRX_DIAG_NoCodeCoverageInfo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No test results files found.
        /// </summary>
        internal static string TRX_DIAG_NoTestResultsFound {
            get {
                return ResourceManager.GetString("TRX_DIAG_NoTestResultsFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;sonar.cs.vstest.reportsPaths&apos; provided, skipping the search for TRX files in default folders....
        /// </summary>
        internal static string TRX_DIAG_SkippingCoverageCheckPropertyProvided {
            get {
                return ResourceManager.GetString("TRX_DIAG_SkippingCoverageCheckPropertyProvided", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test results directory does not exist in {0}.
        /// </summary>
        internal static string TRX_DIAG_TestResultsDirectoryNotFound {
            get {
                return ResourceManager.GetString("TRX_DIAG_TestResultsDirectoryNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The following test results files were found: {0}.
        /// </summary>
        internal static string TRX_DIAG_TrxFilesFound {
            get {
                return ResourceManager.GetString("TRX_DIAG_TrxFilesFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to None of the attached coverage reports exist on disk.
        /// </summary>
        internal static string TRX_WARN_CoverageAttachmentsNotFound {
            get {
                return ResourceManager.GetString("TRX_WARN_CoverageAttachmentsNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to None of the following coverage attachments could be found: {0}. Trx file: {1}.
        /// </summary>
        internal static string TRX_WARN_InvalidConstructedCoveragePath {
            get {
                return ResourceManager.GetString("TRX_WARN_InvalidConstructedCoveragePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Located trx file is not a valid xml file. File: {0}. File load error: {1}.
        /// </summary>
        internal static string TRX_WARN_InvalidTrx {
            get {
                return ResourceManager.GetString("TRX_WARN_InvalidTrx", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ** WARNING: Support for XAML builds is deprecated since version 4.1 and will be removed in version 5.0 of the Scanner for MSBuild **.
        /// </summary>
        internal static string WARN_XamlBuildDeprecated {
            get {
                return ResourceManager.GetString("WARN_XamlBuildDeprecated", resourceCulture);
            }
        }
    }
}
