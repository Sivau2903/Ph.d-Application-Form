using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Diagnostics.Instrumentation.Extensions.Intercept;

namespace Ph.d_Application_Form.Models
{
    public class ApplicationFormHeaderViewModel
    {
        public University University { get; set; }
        public School School { get; set; }
        public Applicant Appli { get; set; } = new Applicant();
        public ApplicantBackgroundDetail Back { get; set; } = new ApplicantBackgroundDetail();
        public AdditionalPhDDetail Applicant { get; set; } = new AdditionalPhDDetail();
        public List<ApplicantAcademicRecord> AcademicRecords { get; set; } = new List<ApplicantAcademicRecord>();

        public AdditionalPhDDetail additional { get; set; } = new AdditionalPhDDetail();
        public List<ArticlesAndResearch> Articles { get; set; } = new List<ArticlesAndResearch>();
        public List<AwardsAndRecognition> Awards { get; set; } = new List<AwardsAndRecognition>();
        public List<LanguageProficiency> Languages { get; set; } = new List<LanguageProficiency>();
        public List<Publication> Publication { get; set; } = new List<Publication>();
        public PhDQuestionnaire AssociatedWithICFAI { get; set; } = new PhDQuestionnaire();

        public PhDEmploymentAndResearchDetail Emp { get; set; } = new PhDEmploymentAndResearchDetail();

        public List<Reference> References { get; set; } = new List<Reference>();
        public List<SeminarOrConference> Seminars { get; set; } = new List<SeminarOrConference>();
        public List<WorkExperience> WorkExperience { get; set; } = new List<WorkExperience>();

        // Fixed the placement of the 'public' modifier  
        public string GenerateCaptcha()
        {
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

}
