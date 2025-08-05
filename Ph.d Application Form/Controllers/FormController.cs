using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ph.d_Application_Form.Models;

namespace Ph.d_Application_Form.Controllers
{
    public class FormController : Controller
    {
        private readonly PHDEntities db = new PHDEntities();
        // GET: Form  
        public ActionResult Index()
        {
            var universities = db.Universities.ToList();
            var schools = db.Schools
                .Select(s => new
                {
                    s.SchoolID,
                    s.SchoolName,
                    UniversityID = s.UniversityID ?? 0
                })
                .ToList();

            var groupedSchools = schools
                .GroupBy(s => s.UniversityID)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Select(s => new SchoolDto
                    {
                        SchoolID = s.SchoolID,
                        SchoolName = s.SchoolName
                    }).ToList()
                );

            var viewModel = new UniversitySchoolViewModel
            {
                Universities = universities,
                SchoolData = groupedSchools
            };

            return View(viewModel);
        }



        [HttpPost]
        public ActionResult OpenApplicationForm(int UniversityID, int? SchoolID)
        {
            // Redirect to application form  
            return RedirectToAction("ApplicationForm", new { universityId = UniversityID, schoolId = SchoolID });
        }




        [HttpGet]
        public ActionResult ApplicationForm(int universityId, int? schoolId, int step = 1)
        {
            ViewBag.CurrentStep = step;

            System.Diagnostics.Debug.WriteLine("===== [GET] ApplicationForm HIT =====");
            System.Diagnostics.Debug.WriteLine("Received UniversityID: " + universityId);
            System.Diagnostics.Debug.WriteLine("Received SchoolID: " + (schoolId.HasValue ? schoolId.ToString() : "NULL"));
            System.Diagnostics.Debug.WriteLine("Current Step: " + step);

            var university = db.Universities.FirstOrDefault(u => u.UniversityID == universityId);
            var school = schoolId.HasValue ? db.Schools.FirstOrDefault(s => s.SchoolID == schoolId.Value) : null;

            var viewModel = new ApplicationFormHeaderViewModel
            {
                University = university,
                School = school,
                Appli = new Applicant(),
                AcademicRecords = new List<ApplicantAcademicRecord>(),
                additional = new AdditionalPhDDetail(),
                WorkExperience = new List<WorkExperience>(),
                Awards = new List<AwardsAndRecognition>(),
                Languages = new List<LanguageProficiency>(),
                Articles = new List<ArticlesAndResearch>(),
                Seminars = new List<SeminarOrConference>(),
                References = new List<Reference>(),
                Publication = new List<Publication>(),
                Emp = new PhDEmploymentAndResearchDetail(),
                AssociatedWithICFAI = new PhDQuestionnaire(),
                Back = new ApplicantBackgroundDetail()
            };

            int applicantId = 0;
            if (Session["ApplicantID"] != null)
            {
                applicantId = (int)Session["ApplicantID"];
                System.Diagnostics.Debug.WriteLine("Applicant ID from session: " + applicantId);

                switch (step)
                {
                    case 1:
                        viewModel.Appli = db.Applicants.FirstOrDefault(a => a.ApplicantID == applicantId);
                        System.Diagnostics.Debug.WriteLine("Loaded Applicant basic info.");
                        break;

                    case 2:
                        viewModel.AcademicRecords = db.ApplicantAcademicRecords
                                                      .Where(a => a.ApplicantID == applicantId)
                                                      .ToList();

                        // Ensure 5 academic levels always show
                        if (viewModel.AcademicRecords == null || !viewModel.AcademicRecords.Any())
                        {
                            viewModel.AcademicRecords = new List<ApplicantAcademicRecord>();
                            foreach (var level in new[] { "X", "XII", "Graduation", "PostGraduation", "Others" })
                            {
                                viewModel.AcademicRecords.Add(new ApplicantAcademicRecord
                                {
                                    ApplicantID = applicantId,
                                    EducationLevel = level
                                });
                            }
                        }

                        System.Diagnostics.Debug.WriteLine("Loaded Academic Records count: " + viewModel.AcademicRecords.Count);
                        break;

                    case 3:
                        viewModel.additional = db.AdditionalPhDDetails.FirstOrDefault(a => a.ApplicantID == applicantId);
                        System.Diagnostics.Debug.WriteLine("Loaded Additional PhD Detail.");
                        break;

                    case 4:
                        viewModel.WorkExperience = db.WorkExperiences
                                                     .Where(w => w.ApplicantID == applicantId)
                                                     .ToList();
                        viewModel.Awards = db.AwardsAndRecognitions
                                             .Where(a => a.ApplicantID == applicantId)
                                             .ToList();
                        viewModel.Languages = db.LanguageProficiencies
                                                .Where(l => l.ApplicantID == applicantId)
                                                .ToList();

                        // Add empty items if any list is empty
                        if (viewModel.WorkExperience.Count == 0)
                            viewModel.WorkExperience.Add(new WorkExperience());

                        if (viewModel.Awards.Count == 0)
                            viewModel.Awards.Add(new AwardsAndRecognition());

                        if (viewModel.Languages.Count == 0)
                            viewModel.Languages.Add(new LanguageProficiency());

                        System.Diagnostics.Debug.WriteLine("Loaded Work Experience: "
                            + viewModel.WorkExperience.Count
                            + ", Awards: "
                            + viewModel.Awards.Count
                            + ", Languages: "
                            + viewModel.Languages.Count);
                        break;

                    case 5:
                        viewModel.Articles = db.ArticlesAndResearches
                                               .Where(a => a.ApplicantID == applicantId)
                                               .ToList();
                        viewModel.Seminars = db.SeminarOrConferences
                                               .Where(s => s.ApplicantID == applicantId)
                                               .ToList();
                        viewModel.References = db.References
                                                 .Where(r => r.ApplicantID == applicantId)
                                                 .ToList();

                        System.Diagnostics.Debug.WriteLine("Loaded Articles: "
                            + viewModel.Articles.Count
                            + ", Seminars: " + viewModel.Seminars.Count
                            + ", References: " + viewModel.References.Count);

                        // Ensure at least 1 Article row
                        if (viewModel.Articles.Count == 0)
                            viewModel.Articles.Add(new ArticlesAndResearch());

                        // Ensure at least 1 Seminar row
                        if (viewModel.Seminars.Count == 0)
                            viewModel.Seminars.Add(new SeminarOrConference());

                        // Ensure 2 Reference rows minimum
                        while (viewModel.References.Count < 2)
                            viewModel.References.Add(new Reference());
                        break;

                    case 6:
                        viewModel.WorkExperience = db.WorkExperiences
                                                    .Where(w => w.ApplicantID == applicantId)
                                                    .ToList();
                        viewModel.Publication = db.Publications
                                                  .Where(p => p.ApplicantID == applicantId)
                                                  .ToList();
                        viewModel.References = db.References
                                                 .Where(r => r.ApplicantID == applicantId)
                                                 .ToList();

                        System.Diagnostics.Debug.WriteLine("Loaded Work Experience: "
                            + viewModel.WorkExperience.Count
                            + ", Publications: "
                            + viewModel.Publication.Count
                            + ", References: "
                            + viewModel.References.Count);

                        // Ensure at least 1 WorkExperience row
                        if (viewModel.WorkExperience.Count == 0)
                            viewModel.WorkExperience.Add(new WorkExperience());

                        // Ensure at least 1 Publication row
                        if (viewModel.Publication.Count == 0)
                            viewModel.Publication.Add(new Publication());

                        // Ensure exactly 2 Reference rows
                        while (viewModel.References.Count < 2)
                            viewModel.References.Add(new Reference());
                        break;

                    case 7:
                        viewModel.Emp = db.PhDEmploymentAndResearchDetails.FirstOrDefault(p => p.ApplicantID == applicantId);
                        System.Diagnostics.Debug.WriteLine("Loaded Employment and Research Details.");
                        break;

                    case 8:
                        viewModel.AssociatedWithICFAI = db.PhDQuestionnaires.FirstOrDefault(q => q.ApplicantID == applicantId);
                        System.Diagnostics.Debug.WriteLine("Loaded ICFAI Questionnaire.");
                        break;

                    case 9:
                        viewModel.Back = db.ApplicantBackgroundDetails.FirstOrDefault(b => b.ApplicantID == applicantId);
                        System.Diagnostics.Debug.WriteLine("Loaded Background Details.");
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine("Unknown step.");
                        break;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Session does not contain ApplicantID.");
            }

            // ✅ Check if current step is posted
            bool isPosted = false;
            if (applicantId > 0)
            {
                switch (step)
                {
                    case 1:
                        isPosted = db.Applicants.Any(a => a.ApplicantID == applicantId);
                        break;
                    case 2:
                        isPosted = db.ApplicantAcademicRecords.Any(a => a.ApplicantID == applicantId);
                        break;
                    case 3:
                        isPosted = db.AdditionalPhDDetails.Any(a => a.ApplicantID == applicantId);
                        break;
                    case 4:
                        isPosted =
                            db.WorkExperiences.Any(w => w.ApplicantID == applicantId) ||
                            db.AwardsAndRecognitions.Any(a => a.ApplicantID == applicantId) ||
                            db.LanguageProficiencies.Any(l => l.ApplicantID == applicantId);
                        break;
                    case 5:
                        isPosted =
                            db.ArticlesAndResearches.Any(a => a.ApplicantID == applicantId) ||
                            db.SeminarOrConferences.Any(s => s.ApplicantID == applicantId) ||
                            db.References.Any(r => r.ApplicantID == applicantId);
                        break;
                    case 6:
                        isPosted =
                            db.WorkExperiences.Any(w => w.ApplicantID == applicantId) ||
                            db.Publications.Any(p => p.ApplicantID == applicantId) ||
                            db.References.Any(r => r.ApplicantID == applicantId);
                        break;
                    case 7:
                        isPosted = db.PhDEmploymentAndResearchDetails.Any(p => p.ApplicantID == applicantId);
                        break;
                    case 8:
                        isPosted = db.PhDQuestionnaires.Any(q => q.ApplicantID == applicantId);
                        break;
                    case 9:
                        isPosted = db.ApplicantBackgroundDetails.Any(b => b.ApplicantID == applicantId);
                        break;
                }
            }
            ViewBag.IsStepPosted = isPosted;
            ViewBag.CurrentStep = step;
            // ✅ Step Navigation
            ViewBag.PreviousStep = GetPreviousVisibleStep(universityId, schoolId, step);
            ViewBag.NextStep = GetNextVisibleStep(universityId, schoolId, step);

            return View(viewModel);
        }





        private int GetNextVisibleStep(int universityId, int? schoolId, int currentStep)
        {
            List<int> visibleSteps = new List<int>();

            if (schoolId.HasValue)
            {
                if (schoolId == 2)
                    visibleSteps = new List<int> { 1, 2, 6, 7, 9 };
                else if (schoolId == 1 || schoolId == 4)
                    visibleSteps = new List<int> { 1, 2, 4, 8, 9 };
                else if (schoolId == 3)
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
            }
            else
            {
                if (new[] { 2, 6, 7, 8 }.Contains(universityId))
                    visibleSteps = new List<int> { 1,2, 3, 9 };
                else if (new[] { 3, 4, 5 }.Contains(universityId))
                    visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };
            }

            int index = visibleSteps.IndexOf(currentStep);
            return (index >= 0 && index + 1 < visibleSteps.Count) ? visibleSteps[index + 1] : currentStep;
        }




        [HttpPost]
        public ActionResult ApplicationForm(ApplicationFormHeaderViewModel viewModel, FormCollection form, HttpPostedFileBase PhotoFile, HttpPostedFileBase SignatureFile)
        {
            int step = Convert.ToInt32(form["step"]);
            System.Diagnostics.Debug.WriteLine("===== POST ApplicationForm method HIT =====");
            System.Diagnostics.Debug.WriteLine($"Step received in POST: {step}");
            System.Diagnostics.Debug.WriteLine($"Form keys received: {string.Join(", ", Request.Form.AllKeys)}");
            System.Diagnostics.Debug.WriteLine($"University ID: {viewModel.University?.UniversityID}");
            System.Diagnostics.Debug.WriteLine($"School ID: {viewModel.School?.SchoolID}");
            //System.Diagnostics.Debug.WriteLine($"School ID: {viewModel.Appli?.ApplicantID}");


            int universityId = viewModel.University?.UniversityID ?? 0;
            int? schoolId = viewModel.School?.SchoolID;
            int applicantId = viewModel.Appli?.ApplicantID ?? 0;

            string programMode = viewModel.Appli.ProgramMode?.Trim();
            int courseId = viewModel.Appli.CourseID ?? 0;
          

            //// Basic validation
            //if (universityId == 0 || schoolId == null || courseId == 0)
            //{
            //    ModelState.AddModelError("", "University, School, and Course must be selected.");
            //    return View(viewModel);
            //}

            // If no ApplicantID yet, assign it after Step 1
            //int applicantId = Session["ApplicantID"] != null ? (int)Session["ApplicantID"] : 0;
            //int applicantId = 0;

                if (Session["ApplicantID"] != null)
                    applicantId = (int)Session["ApplicantID"];
                else if (TempData["ApplicantID"] != null)
                    applicantId = (int)TempData["ApplicantID"];
            


            System.Diagnostics.Debug.WriteLine($"Resolved Applicant ID: {applicantId}");


            System.Diagnostics.Debug.WriteLine($"Checking visible steps for universityId: {universityId}, schoolId: {schoolId}");

            // Determine visible steps
            List<int> visibleSteps = null;

            // ✅ STEP 1: Determine and store visibleSteps in TempData (only during step 1)
            if (step == 1)
            {
                visibleSteps = new List<int>();

                if (schoolId == 2)
                    visibleSteps = new List<int> { 1, 2, 6, 7, 9 };
                else if (schoolId == 1 || schoolId == 4)
                    visibleSteps = new List<int> { 1, 2, 4, 8, 9 };
                else if (schoolId == 3)
                    visibleSteps = new List<int> { 1, 2, 3, 9 };

                if (visibleSteps.Count == 0)
                {
                    if (new[] { 2, 6, 7, 8 }.Contains(universityId))
                        visibleSteps = new List<int> { 1, 2, 3, 9 };
                    else if (new[] { 3, 4, 5 }.Contains(universityId))
                        visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };
                }

                System.Diagnostics.Debug.WriteLine($"Storing visibleSteps to TempData: {string.Join(", ", visibleSteps)}");
                TempData["VisibleSteps"] = visibleSteps;
            }
            else
            {
                // ✅ STEP 2+: Retrieve visibleSteps from TempData
                if (TempData["VisibleSteps"] != null)
                {
                    visibleSteps = TempData["VisibleSteps"] as List<int>;
                    System.Diagnostics.Debug.WriteLine($"Retrieved visibleSteps from TempData: {string.Join(", ", visibleSteps)}");

                    visibleSteps = TempData["VisibleSteps"] as List<int>;
                    TempData.Keep("VisibleSteps"); // ✅ Immediately after first access
                    System.Diagnostics.Debug.WriteLine($"Retrieved visibleSteps from TempData: {string.Join(", ", visibleSteps)}");
                    // 👈 KEEP IT ALIVE

                    if (!visibleSteps.Contains(step))
                    {
                        System.Diagnostics.Debug.WriteLine($"ERROR: Step {step} is not in visibleSteps: {string.Join(", ", visibleSteps)}");
                        return RedirectToAction("ApplicationForm", new { step = 1 });
                    }
                }

                else
                {
                    // ❌ Handle if TempData expired
                    System.Diagnostics.Debug.WriteLine("ERROR: TempData[\"VisibleSteps\"] is null. Cannot determine visibility.");
                    return RedirectToAction("ApplicationForm", new { step = 1 });
                }
            }


            //visibleSteps = TempData["VisibleSteps"] as List<int>;
            //System.Diagnostics.Debug.WriteLine($"Retrieved visibleSteps from TempData: {string.Join(", ", visibleSteps)}");
            //TempData.Keep("VisibleSteps"); // ✅ KEEP IT ALIVE for next step

            // Keep for next request

            // ✅ STEP 3: Check if current step is in visibleSteps
            if (!visibleSteps.Contains(step))
            {
                System.Diagnostics.Debug.WriteLine($"Step {step} is NOT in visibleSteps. Recalculating visibility...");

                // Optional fallback visibility logic here if needed...
                return RedirectToAction("ApplicationForm", new { step = 1 }); // or show error
            }

            System.Diagnostics.Debug.WriteLine("STEP is in visibleSteps — entering SWITCH");

            if (visibleSteps.Contains(step))
            {
                System.Diagnostics.Debug.WriteLine("STEP is in visibleSteps — entering SWITCH");

                switch (step)
                {
                    case 1:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 1 block =====");
                        if (schoolId == 0)
                        {
                            viewModel.Appli.SchoolID = null;
                            ModelState.Remove("Appli.SchoolID"); // ✅ Remove required validation
                        }
                        else
                        {
                            viewModel.Appli.SchoolID = schoolId;
                        }


                        viewModel.Appli.UniversityID = universityId;
                        //viewModel.Appli.SchoolID = (schoolId > 0) ? schoolId : null;
                        viewModel.Appli.ProgramMode = programMode;
                        viewModel.Appli.CourseID = courseId;

                        if (PhotoFile != null && PhotoFile.ContentLength > 0)
                        {
                            string fileName = Path.GetFileName(PhotoFile.FileName);
                            string path = Path.Combine(Server.MapPath("~/Uploads/Photos"), fileName);
                            PhotoFile.SaveAs(path);
                            viewModel.Appli.PhotoPath = "~/Uploads/Photos/" + fileName;
                        }

                        if (SignatureFile != null && SignatureFile.ContentLength > 0)
                        {
                            string fileName = Path.GetFileName(SignatureFile.FileName);
                            string path = Path.Combine(Server.MapPath("~/Uploads/Signatures"), fileName);
                            SignatureFile.SaveAs(path);
                            viewModel.Appli.SignaturePath = "~/Uploads/Signatures/" + fileName;
                        }

                        db.Applicants.Add(viewModel.Appli);
                        try
                        {
                            db.SaveChanges();
                        }
                        catch (DbEntityValidationException ex)
                        {
                            foreach (var validationErrors in ex.EntityValidationErrors)
                            {
                                foreach (var error in validationErrors.ValidationErrors)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Property: {error.PropertyName} Error: {error.ErrorMessage}");
                                }
                            }
                            throw; // rethrow the original exception after logging
                        }


                        applicantId = viewModel.Appli.ApplicantID;
                        Session["ApplicantID"] = applicantId;
                        TempData["ApplicantID"] = applicantId; // <-- Save for next step
                        break;


                    case 2:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 2 block =====");
                        System.Diagnostics.Debug.WriteLine($"AcademicRecords Count: {viewModel.AcademicRecords?.Count ?? 0}");

                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        foreach (var record in viewModel.AcademicRecords)
                        {
                            viewModel.Appli.UniversityID = universityId;
                            viewModel.Appli.SchoolID = (schoolId > 0) ? schoolId : null;
                            record.ApplicantID = applicantId;
                            db.ApplicantAcademicRecords.Add(record);
                        }
                        break;


                    case 3:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 3 block =====");
                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }
                        viewModel.additional.UniversityID = universityId;
                        viewModel.additional.SchoolID = (schoolId > 0) ? schoolId : null;
                        viewModel.additional.ApplicantID = applicantId;
                        db.AdditionalPhDDetails.Add(viewModel.additional);
                        break;

                    case 4:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 4 block =====");

                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // Save Work Experience
                        foreach (var exp in viewModel.WorkExperience)
                        {
                            exp.ApplicantID = applicantId;
                            exp.UniversityID = universityId; // ✅ Set FK
                            exp.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.WorkExperiences.Add(exp);
                        }

                        // Save Awards and Recognitions
                        foreach (var award in viewModel.Awards)
                        {
                            award.ApplicantID = applicantId;
                            award.UniversityID = universityId; // ✅ Set FK
                            award.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.AwardsAndRecognitions.Add(award);
                        }

                        // Save Languages
                        foreach (var lang in viewModel.Languages)
                        {
                            lang.ApplicantID = applicantId;
                            lang.UniversityID = universityId; // ✅ Set FK
                            lang.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.LanguageProficiencies.Add(lang);
                        }

                        break;

                    case 5:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 5 block =====");

                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // Save Articles
                        foreach (var article in viewModel.Articles)
                        {
                            article.ApplicantID = applicantId;
                            article.UniversityID = universityId; // ✅ Set FK
                            article.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.ArticlesAndResearches.Add(article);
                        }

                        // Save Seminars
                        foreach (var seminar in viewModel.Seminars)
                        {
                            seminar.ApplicantID = applicantId;
                            seminar.UniversityID = universityId; // ✅ Set FK
                            seminar.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.SeminarOrConferences.Add(seminar);
                        }

                        // Save References
                        foreach (var reference in viewModel.References)
                        {
                            reference.ApplicantID = applicantId;
                            reference.UniversityID = universityId; // ✅ Set FK
                            reference.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.References.Add(reference);
                        }

                        break;


                    case 6:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 6 block =====");

                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // Save Work Experience
                        foreach (var exp in viewModel.WorkExperience)
                        {
                            exp.ApplicantID = applicantId;
                            exp.UniversityID = universityId; // ✅ Set FK
                            exp.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.WorkExperiences.Add(exp);
                        }

                        // Save Publications
                        foreach (var pub in viewModel.Publication)
                        {
                            pub.ApplicantID = applicantId;
                            pub.UniversityID = universityId; // ✅ Set FK
                            pub.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.Publications.Add(pub);
                        }

                        // Save References
                        foreach (var reference in viewModel.References)
                        {
                            reference.ApplicantID = applicantId;
                            reference.UniversityID = universityId; // ✅ Set FK
                            reference.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            db.References.Add(reference);
                        }

                        break;


                    case 7:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 7 block =====");
                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // ✅ Ensure UniversityID and ApplicantID are valid
                        viewModel.Emp.ApplicantID = applicantId;
                        viewModel.Emp.UniversityID = universityId; // <-- ADD THIS LINE
                        viewModel.Emp.SchoolID = (schoolId > 0) ? schoolId : null;

                        // Debug info
                        System.Diagnostics.Debug.WriteLine($"Emp.ApplicantID = {viewModel.Emp.ApplicantID}");
                        System.Diagnostics.Debug.WriteLine($"Emp.UniversityID = {viewModel.Emp.UniversityID}");
                        System.Diagnostics.Debug.WriteLine($"Emp.SchoolID = {viewModel.Emp.SchoolID}");

                        db.PhDEmploymentAndResearchDetails.Add(viewModel.Emp);
                        break;


                    case 8:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 8 block =====");

                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // ✅ Ensure AssociatedWithICFAI is not null
                        if (viewModel.AssociatedWithICFAI == null)
                            viewModel.AssociatedWithICFAI = new PhDQuestionnaire();

                        viewModel.AssociatedWithICFAI.UniversityID = universityId;
                        viewModel.AssociatedWithICFAI.SchoolID = (schoolId > 0) ? schoolId : null;
                        viewModel.AssociatedWithICFAI.ApplicantID = applicantId;

                        db.PhDQuestionnaires.Add(viewModel.AssociatedWithICFAI);
                        break;


                    case 9:
                        System.Diagnostics.Debug.WriteLine("===== POST: Entered STEP 9 block =====");

                        // ✅ Retrieve ApplicantID from TempData
                        if (TempData["ApplicantID"] != null)
                        {
                            applicantId = (int)TempData["ApplicantID"];
                            TempData.Keep("ApplicantID");
                        }

                        // ✅ Ensure UniversityID is valid
                        if (universityId <= 0)
                            universityId = viewModel.University?.UniversityID ?? 0;

                        if (universityId <= 0)
                        {
                            throw new Exception("UniversityID is missing for Step 9 submission!");
                        }

                        // ✅ Assign foreign keys
                        viewModel.Back.UniversityID = universityId;
                        viewModel.Back.SchoolID = (schoolId > 0) ? schoolId : null;
                        viewModel.Back.ApplicantID = applicantId;

                        // ✅ Save final background details
                        db.ApplicantBackgroundDetails.Add(viewModel.Back);
                        db.SaveChanges();

                        // ✅ Clear all TempData to restart application
                        TempData.Clear();

                        // ✅ Optional: Clear Session keys if stored there
                        Session["UniversityID"] = null;
                        Session["SchoolID"] = null;
                        Session["ApplicantID"] = null;

                        // ✅ Return JSON for success popup
                        return Json(new { success = true, message = "Application submitted successfully!" });

                    default:
                        System.Diagnostics.Debug.WriteLine($"Step matched no case: {step}");
                        break;
                }

                db.SaveChanges();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("STEP NOT IN visibleSteps — skipping switch-case block.");
            }
          
                // applicantId = (int)Session["ApplicantID"];
                //var existing = db.Applicants.Find(applicantId);
                //if (existing != null)
                //{
                //    db.Entry(existing).CurrentValues.SetValues(viewModel.Appli);
                //    db.SaveChanges();
                //}
            
            if (schoolId == null || schoolId == 0)
            {
                schoolId = db.Applicants.Where(a => a.ApplicantID == applicantId)
                                        .Select(a => a.SchoolID)
                                        .FirstOrDefault();
            }

            int nextStep = GetNextVisibleStep(universityId, schoolId, step);
            System.Diagnostics.Debug.WriteLine($"Redirecting to ApplicationForm GET with step = {nextStep}, universityId = {universityId}, schoolId = {schoolId}");

            return RedirectToAction("ApplicationForm", new { universityId, schoolId, step = nextStep });
        }



        private int GetPreviousVisibleStep(int universityId, int? schoolId, int currentStep)
        {
            List<int> visibleSteps = new List<int>();

            if (schoolId.HasValue)
            {
                if (schoolId == 2)
                    visibleSteps = new List<int> { 1, 2, 6, 7, 9 };
                else if (schoolId == 1 || schoolId == 4)
                    visibleSteps = new List<int> { 1, 2, 4, 8, 9 };
                else if (schoolId == 3)
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
            }
            else
            {
                if (new[] { 2, 6, 7, 8 }.Contains(universityId))
                    visibleSteps = new List<int> { 1,2, 3, 9 };
                else if (new[] { 3, 4, 5 }.Contains(universityId))
                    visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };
            }

            int index = visibleSteps.IndexOf(currentStep);
            return (index > 0) ? visibleSteps[index - 1] : currentStep;
        }


    }
}