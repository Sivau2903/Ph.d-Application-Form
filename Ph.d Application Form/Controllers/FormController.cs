using Ph.d_Application_Form.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Web;
using System.Web.Mvc;

namespace Ph.d_Application_Form.Controllers
{
    public class FormController : Controller
    {
        private readonly PHDEntities db = new PHDEntities();
        // GET: Form  


        public ActionResult Login()
        {
            return View();
        }

        public ActionResult GenerateCaptcha()
        {
            string captchaText = new Random().Next(1000, 9999).ToString();
            Session["Captcha"] = captchaText;

            using (Bitmap bmp = new Bitmap(100, 40))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.DrawString(captchaText, new Font("Arial", 20), Brushes.Black, new PointF(10, 5));
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
                }
            }
        }

        [HttpPost]
        public ActionResult Login(LoginViewModel model)
        {
            string captcha = Session["Captcha"]?.ToString();
            if (model.CaptchaCode != captcha)
            {
                ViewBag.ErrorMessage = "Invalid Captcha";
                return View(model);
            }

            var user = db.Users.FirstOrDefault(u => u.EmailID == model.Username);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Email ID not found";
                return View(model);
            }

            // Decrypt stored DOB
            string decryptedPassword = SecureHelper.Decrypt(user.PasswordHash);

            if (!DateTime.TryParse(model.Password, out DateTime inputDob))
            {
                ViewBag.ErrorMessage = "Invalid Date Format";
                return View(model);
            }

            if (decryptedPassword != inputDob.ToString("yyyyMMdd"))
            {
                ViewBag.ErrorMessage = "Invalid Date of Birth";
                return View(model);
            }

            // ✅ Store UserID and Role in session
            Session["UserID"] = user.UserID;
            Session["Role"] = user.Role; // fetch role from Users table

            // ✅ Redirect based on Role stored in Users table
            if (user.Role == "Admin")
            {
                return RedirectToAction("AdminDasboard", "Form");
            }
            else // assume applicant
            {
                return RedirectToAction("Dashboard", "Form");
            }
        }


        public bool ValidateUser(string email, string dobInput)
        {
            var user = db.Users.FirstOrDefault(u => u.PasswordHash == email);
            if (user == null) return false;

            // Normalize DOB format
            DateTime parsedDob;
            if (!DateTime.TryParse(dobInput, out parsedDob))
                return false;

            string formattedDob = parsedDob.ToString("yyyyMMdd"); // match storage format
            string encryptedDob = SecureHelper.Encrypt(formattedDob);

            return encryptedDob == user.PasswordHash;
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Form");
        }

        public ActionResult Dashboard()
        {
            int userId = Convert.ToInt32(Session["UserID"]);

            using (var db = new PHDEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.UserID == userId);
                if (user == null)
                {
                    return RedirectToAction("Login", "Form");
                }

                var applicant = db.Applicants.FirstOrDefault(a => a.ApplicantID == user.ApplicantID);
                if (applicant == null)
                {
                    return RedirectToAction("Login", "Form");
                }

                //var visibleSteps = Session["VisibleSteps"] as List<int> ?? new List<int>();
                //var visibleSteps = new List<int> { 1, 2, 3 };

                var model = new DashboardViewModel
                {
                    ApplicantID = applicant.ApplicantID,
                    Name = applicant.FullName,
                    EmailID = applicant.Email,
                    PhoneNumber = applicant.MobileNumber,
                    Status = user.status,
                    PhotoPath = applicant.PhotoPath,
                    VisibleSteps = string.IsNullOrEmpty(user.VisibleSteps)
          ? new List<int>()
          : user.VisibleSteps.Split(',')
              .Select(int.Parse)
              .ToList(),
                };



                return View(model);
            }
        }

        [HttpGet]
        public ActionResult GetStepDetails(int step, int applicantId)
        {
            var viewModel = new ApplicationFormHeaderViewModel();

            switch (step)
            {
                case 1: // ✅ Personal
                    var appli = db.Applicants.FirstOrDefault(a => a.ApplicantID == applicantId);
                    if (appli == null) return PartialView("_NoDataFound");
                    return PartialView("_PersonalDetails", appli);

                case 2: // ✅ Academic
                    var academics = db.ApplicantAcademicRecords
                        .Where(r => r.ApplicantID == applicantId)
                        .ToList();
                    if (!academics.Any()) return PartialView("_NoDataFound");
                    return PartialView("_AcademicDetails", academics);

                case 3: // ✅ Additional
                    var additional = db.AdditionalPhDDetails.FirstOrDefault(a => a.ApplicantID == applicantId);
                    if (additional == null) return PartialView("_NoDataFound");
                    return PartialView("_AdditionalDetails", additional);

                case 4: // ✅ Experience + Awards + Languages
                    viewModel.WorkExperience = db.WorkExperiences.Where(w => w.ApplicantID == applicantId).ToList();
                    viewModel.Awards = db.AwardsAndRecognitions.Where(a => a.ApplicantID == applicantId).ToList();
                    viewModel.Languages = db.LanguageProficiencies.Where(l => l.ApplicantID == applicantId).ToList();

                    if (!viewModel.WorkExperience.Any() && !viewModel.Awards.Any() && !viewModel.Languages.Any())
                        return PartialView("_NoDataFound");

                    return PartialView("_ExperienceDetails", viewModel);

                case 5: // ✅ Articles + Seminars + References
                    viewModel.Articles = db.ArticlesAndResearches.Where(a => a.ApplicantID == applicantId).ToList();
                    viewModel.Seminars = db.SeminarOrConferences.Where(s => s.ApplicantID == applicantId).ToList();
                    viewModel.References = db.References.Where(r => r.ApplicantID == applicantId).ToList();

                    if (!viewModel.Articles.Any() && !viewModel.Seminars.Any() && !viewModel.References.Any())
                        return PartialView("_NoDataFound");

                    return PartialView("_ResearchDetails", viewModel);

                case 6: // ✅ Work + Publications + References
                    viewModel.WorkExperience = db.WorkExperiences.Where(w => w.ApplicantID == applicantId).ToList();
                    viewModel.Publication = db.Publications.Where(p => p.ApplicantID == applicantId).ToList();
                    viewModel.References = db.References.Where(r => r.ApplicantID == applicantId).ToList();

                    if (!viewModel.WorkExperience.Any() && !viewModel.Publication.Any() && !viewModel.References.Any())
                        return PartialView("_NoDataFound");

                    return PartialView("_Publications", viewModel);

                case 7: // ✅ Employment & Research
                    var emp = db.PhDEmploymentAndResearchDetails.FirstOrDefault(e => e.ApplicantID == applicantId);
                    if (emp == null) return PartialView("_NoDataFound");
                    return PartialView("_EmploymentResearch", emp);

                case 8: // ✅ Questionnaire
                    var questionnaire = db.PhDQuestionnaires.FirstOrDefault(q => q.ApplicantID == applicantId);
                    if (questionnaire == null) return PartialView("_NoDataFound");
                    return PartialView("_Questionnaire", questionnaire);

                case 9: // ✅ Background
                    var background = db.ApplicantBackgroundDetails.FirstOrDefault(b => b.ApplicantID == applicantId);
                    if (background == null) return PartialView("_NoDataFound");
                    return PartialView("_BackgroundDetails", background);

                default:
                    return new EmptyResult(); // invalid step
            }
        }


        [HttpPost]
        public ActionResult ContinueApplication()
        {
            int userId = Convert.ToInt32(Session["UserID"]);

            using (var db = new PHDEntities())
            {
                var user = db.Users.FirstOrDefault(u => u.UserID == userId);

                if (user == null)
                {
                    return RedirectToAction("Login", "Form");
                }

                // ✅ Fetch University and School IDs
                int universityId = user.UniversityID;
                int? schoolId = user.SchoolID;

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
                        visibleSteps = new List<int> { 1, 2, 3, 9 };
                    else if (new[] { 3, 4, 5 }.Contains(universityId))
                        visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };
                }

                if (!visibleSteps.Any())
                    visibleSteps = new List<int> { 1, 2, 3, 9 };

                int currentStep = (int)user.CurrentStep;
                if (!visibleSteps.Contains(currentStep))
                    currentStep = visibleSteps.First();

                // ✅ Save info in session
                Session["UserID"] = user.UserID;
                Session["UniversityID"] = universityId;
                Session["SchoolID"] = schoolId;
                Session["VisibleSteps"] = visibleSteps;
                Session["CurrentStep"] = currentStep;
                Session["ApplicantID"] = user.ApplicantID;

                // ✅ Redirect user back to where they stopped
                return RedirectToAction("ApplicationForm", "Form", new { universityId, schoolId, step = currentStep });
            }
        }

        [HttpPost]
        public ActionResult Payment()
        {
            int userId = Convert.ToInt32(Session["UserID"]);

            using (var db = new PHDEntities())
            {

                var user = db.Users.FirstOrDefault(u => u.UserID == userId);
                if (user == null) return RedirectToAction("Login", "Form");

                var applicant = db.Applicants.FirstOrDefault(a => a.ApplicantID == user.ApplicantID);
                if (applicant == null) return RedirectToAction("Login", "Form");

                var report = db.ApplicationsReports.FirstOrDefault(a => a.ApplicantID == user.ApplicantID);
                if (report == null) return RedirectToAction("Login", "Form");

                // ✅ Generate Application Number
                string applicationNo = "APP" + DateTime.Now.Year + applicant.ApplicantID.ToString("D4");

                // ✅ Generate Dummy Transaction Number
                string transactionNo = "TXN" + DateTime.Now.Ticks;

                // ✅ Transaction details
                DateTime transactionDate = DateTime.Now;
                string transactionCategory = "ApplicationFee";

                // ✅ Fetch CourseName from PhdCourses
                var course = db.PhDCourses.FirstOrDefault(c => c.CourseID == applicant.CourseID);
                string courseName = course != null ? course.CourseName : "Unknown Program";

                // ✅ Insert into PaymentReport
                var payment = new PaymentReport
                {
                    ApplicantID = applicant.ApplicantID,
                    ApplicationNo = applicationNo,
                    TransactionNo = transactionNo,
                    TransactionDate = transactionDate,
                    TransactionCategory = transactionCategory,
                    RegisteredDate = (DateTime)applicant.InsertedDate,
                    Program = courseName,  // <-- CourseName
                    ProgramType = applicant.ProgramMode,// <-- save ProgramMode here
                    UniversityID = applicant.UniversityID,
                    SchoolID = applicant.SchoolID
                };

                db.PaymentReports.Add(payment);

                // ✅ Update Applicant table with same details
                applicant.ApplicationNo = applicationNo;
                applicant.TransactionNo = transactionNo;
                applicant.TransactionDate = transactionDate;
                applicant.TransactionCategory = transactionCategory;

                report.ApplicationNumber = applicationNo;

                // ✅ Update user status
                user.status = "ApplicationFeePaid";

                db.SaveChanges();

                TempData["PaymentSuccess"] = $"✅ Payment recorded successfully! Transaction No: {transactionNo}";
            }

            return RedirectToAction("Dashboard", "Form");
        }


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
                References = new List<Models.Reference>(),
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
                            viewModel.References.Add(new Models.Reference());
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
                            viewModel.References.Add(new Models.Reference());
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
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
                else if (new[] { 3, 4, 5 }.Contains(universityId))
                    visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };
            }

            int index = visibleSteps.IndexOf(currentStep);
            return (index > 0) ? visibleSteps[index - 1] : currentStep;
        }
        // Add this private method inside the FormController class

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
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
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

            // ✅ Retrieve UniversityID, SchoolID, ApplicantID
            int universityId = Session["UniversityID"] != null ? (int)Session["UniversityID"] : (viewModel.University?.UniversityID ?? 0);
            int? schoolId = Session["SchoolID"] != null ? (int?)Session["SchoolID"] : (viewModel.School?.SchoolID);
            int applicantId = Session["ApplicantID"] != null ? (int)Session["ApplicantID"] : (viewModel.Appli?.ApplicantID ?? 0);

            string programMode = viewModel.Appli.ProgramMode?.Trim();
            int courseId = viewModel.Appli.CourseID ?? 0;

            System.Diagnostics.Debug.WriteLine($"Resolved Applicant ID: {applicantId}");
            System.Diagnostics.Debug.WriteLine($"UniversityID: {universityId}, SchoolID: {schoolId}");

            // ✅ STEP 1: Get or Calculate VisibleSteps from Session
            List<int> visibleSteps = Session["VisibleSteps"] as List<int>;

            if (visibleSteps == null || step == 1) // Recalculate for new users or step 1
            {
                visibleSteps = new List<int>();

                if (schoolId == 2)
                    visibleSteps = new List<int> { 1, 2, 6, 7, 9 };
                else if (schoolId == 1 || schoolId == 4)
                    visibleSteps = new List<int> { 1, 2, 4, 8, 9 };
                else if (schoolId == 3)
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
                else if (new[] { 2, 6, 7, 8 }.Contains(universityId))
                    visibleSteps = new List<int> { 1, 2, 3, 9 };
                else if (new[] { 3, 4, 5 }.Contains(universityId))
                    visibleSteps = new List<int> { 1, 2, 4, 5, 8, 9 };

                Session["VisibleSteps"] = visibleSteps;
                System.Diagnostics.Debug.WriteLine($"Calculated VisibleSteps: {string.Join(",", visibleSteps)}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Retrieved VisibleSteps from Session: {string.Join(",", visibleSteps)}");
            }

            // ✅ STEP 2: Validate if current step is allowed
            if (!visibleSteps.Contains(step))
            {
                System.Diagnostics.Debug.WriteLine($"Step {step} is NOT in VisibleSteps. Redirecting to CurrentStep from Session.");
                int currentStep = Session["CurrentStep"] != null ? (int)Session["CurrentStep"] : visibleSteps.First();
                return RedirectToAction("ApplicationForm", new { universityId, schoolId, step = currentStep });
            }

            // ✅ STEP 3: Enter switch logic
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
                            ModelState.Remove("Appli.SchoolID");
                        }
                        else
                        {
                            viewModel.Appli.SchoolID = schoolId;
                        }

                        viewModel.Appli.UniversityID = universityId;
                        viewModel.Appli.ProgramMode = programMode;
                        viewModel.Appli.CourseID = courseId;
                        viewModel.Appli.InsertedDate = DateTime.Now;

                        // ✅ Save Photo
                        if (PhotoFile != null && PhotoFile.ContentLength > 0)
                        {
                            // Create filename like 123_profile.jpg
                            string fileName = applicantId + "_" + Path.GetFileName(PhotoFile.FileName);

                            // Physical path to save file
                            string path = Path.Combine(Server.MapPath("~/Uploads/Photos"), fileName);
                            PhotoFile.SaveAs(path);

                            // ✅ Save relative path to DB (so it can be directly used in the view)
                            viewModel.Appli.PhotoPath = "/Uploads/Photos/" + fileName;

                        }


                        // ✅ Save Signature
                        if (SignatureFile != null && SignatureFile.ContentLength > 0)
                        {
                            // Create filename like 123_signature.png
                            string fileName = applicantId + "_" + Path.GetFileName(SignatureFile.FileName);

                            // Physical path to save the signature
                            string path = Path.Combine(Server.MapPath("~/Uploads/Signatures"), fileName);
                            SignatureFile.SaveAs(path);

                            // ✅ Save relative path to DB
                            viewModel.Appli.SignaturePath = "/Uploads/Signatures/" + fileName;
                        }


                        // ✅ Save Applicant
                        db.Applicants.Add(viewModel.Appli);
                        db.SaveChanges();

                        applicantId = viewModel.Appli.ApplicantID;
                        Session["ApplicantID"] = applicantId;
                        TempData["ApplicantID"] = applicantId;

                        // ✅ Insert into Users Table
                        var user = new User
                        {
                            ApplicantID = applicantId,
                            EmailID = viewModel.Appli.Email,
                            // With this:
                            PasswordHash = SecureHelper.Encrypt(viewModel.Appli.DateOfBirth?.ToString("yyyyMMdd") ?? ""),


                            UniversityID = universityId,
                            SchoolID = schoolId,
                            VisibleSteps = string.Join(",", visibleSteps),
                            CurrentStep = 1,
                            InsertedDate = DateTime.Now,
                            status = "Inprogress"
                            //IsActive = true
                        };

                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 1);
                        db.Users.Add(user);
                        db.SaveChanges();

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
                            record.InsertedDate = DateTime.Now;
                            record.ApplicantID = applicantId;
                            db.ApplicantAcademicRecords.Add(record);
                        }

                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 2);
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
                        viewModel.additional.InsertedDate = DateTime.Now;
                        db.AdditionalPhDDetails.Add(viewModel.additional);
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 3);
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
                            exp.InsertedDate = DateTime.Now;
                            db.WorkExperiences.Add(exp);
                        }

                        // Save Awards and Recognitions
                        foreach (var award in viewModel.Awards)
                        {
                            award.ApplicantID = applicantId;
                            award.UniversityID = universityId; // ✅ Set FK
                            award.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            award.InsertedDate = DateTime.Now;
                            db.AwardsAndRecognitions.Add(award);
                        }

                        // Save Languages
                        foreach (var lang in viewModel.Languages)
                        {
                            lang.ApplicantID = applicantId;
                            lang.UniversityID = universityId; // ✅ Set FK
                            lang.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            lang.InsertedDate = DateTime.Now;
                            db.LanguageProficiencies.Add(lang);
                        }
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 4);
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
                            article.InsertedDate = DateTime.Now;
                            db.ArticlesAndResearches.Add(article);
                        }

                        // Save Seminars
                        foreach (var seminar in viewModel.Seminars)
                        {
                            seminar.ApplicantID = applicantId;
                            seminar.UniversityID = universityId; // ✅ Set FK
                            seminar.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            seminar.InsertedDate = DateTime.Now;
                            db.SeminarOrConferences.Add(seminar);
                        }

                        // Save References
                        foreach (var reference in viewModel.References)
                        {
                            reference.ApplicantID = applicantId;
                            reference.UniversityID = universityId; // ✅ Set FK
                            reference.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            reference.InsertedDate = DateTime.Now;
                            db.References.Add(reference);
                        }
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 5);
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
                            exp.InsertedDate = DateTime.Now;
                            db.WorkExperiences.Add(exp);
                        }

                        // Save Publications
                        foreach (var pub in viewModel.Publication)
                        {
                            pub.ApplicantID = applicantId;
                            pub.UniversityID = universityId; // ✅ Set FK
                            pub.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            pub.InsertedDate = DateTime.Now;
                            db.Publications.Add(pub);
                        }

                        // Save References
                        foreach (var reference in viewModel.References)
                        {
                            reference.ApplicantID = applicantId;
                            reference.UniversityID = universityId; // ✅ Set FK
                            reference.SchoolID = (schoolId > 0) ? schoolId : null; // ✅ Set FK
                            reference.InsertedDate = DateTime.Now;
                            db.References.Add(reference);
                        }
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 6);
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
                        viewModel.Emp.InsertedDate = DateTime.Now;

                        // Debug info
                        System.Diagnostics.Debug.WriteLine($"Emp.ApplicantID = {viewModel.Emp.ApplicantID}");
                        System.Diagnostics.Debug.WriteLine($"Emp.UniversityID = {viewModel.Emp.UniversityID}");
                        System.Diagnostics.Debug.WriteLine($"Emp.SchoolID = {viewModel.Emp.SchoolID}");

                        db.PhDEmploymentAndResearchDetails.Add(viewModel.Emp);
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 7);
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
                        viewModel.AssociatedWithICFAI.InsertedDate = DateTime.Now;
                        db.PhDQuestionnaires.Add(viewModel.AssociatedWithICFAI);
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 8);
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


                        viewModel.Back.InsertedDate = DateTime.Now;
                        // ✅ Save final background details
                        db.ApplicantBackgroundDetails.Add(viewModel.Back);
                        db.SaveChanges();
                        var user2 = db.Users.FirstOrDefault(u => u.ApplicantID == applicantId);
                        if (user2 != null)
                        {
                            user2.status = "Completed";  // Or any value you want, e.g., "Submitted"
                            db.SaveChanges();
                        }
                        // Create ApplicantsReports entry
                        UpdateApplicantsReport(applicantId, universityId, schoolId, 9);
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
            var existingUser = db.Users.FirstOrDefault(u => u.ApplicantID == applicantId);
            if (existingUser != null)
            {
                existingUser.CurrentStep = nextStep;
                db.SaveChanges();
            }

            Session["CurrentStep"] = nextStep;

            System.Diagnostics.Debug.WriteLine($"Redirecting to ApplicationForm GET with step = {nextStep}, universityId = {universityId}, schoolId = {schoolId}");

            return RedirectToAction("ApplicationForm", new { universityId, schoolId, step = nextStep });
        }


        private void UpdateApplicantsReport(int applicantId, int universityId, int? schoolId, int step)
        {
            var report = db.ApplicationsReports.FirstOrDefault(r => r.ApplicantID == applicantId);

            // ✅ STEP 1: Fetch allowed steps
            string stepsString;

            if (schoolId.HasValue && schoolId > 0)
            {
                // Fetch from School table
                stepsString = db.Schools
                                .Where(s => s.SchoolID == schoolId.Value)
                                .Select(s => s.Steps)
                                .FirstOrDefault();
            }
            else
            {
                // Fetch from University table
                stepsString = db.Universities
                                .Where(u => u.UniversityID == universityId)
                                .Select(u => u.Steps)
                                .FirstOrDefault();
            }

            List<int> allowedSteps = stepsString?.Split(',')
                                                 .Where(x => !string.IsNullOrEmpty(x))
                                                 .Select(int.Parse)
                                                 .ToList() ?? new List<int>();

            // ✅ STEP 2: If first time (Step1), insert new row
            if (step == 1 && report == null)
            {
                report = new ApplicationsReport
                {
                    ApplicantID = applicantId,
                    InsertedDate = DateTime.Now,
                    UniversityID = universityId,
                    SchoolID = (schoolId > 0) ? schoolId : null,

                    // Step1 always = 1 when applicant is created
                    PersonalDetails = true,
                    PersonalDetailsDate = DateTime.Now,

                    AcademicDetails = allowedSteps.Contains(2) ? false : (bool?)null,
                    AcademicDetailsDate = null,

                    LastAttended = allowedSteps.Contains(3) ? false : (bool?)null,
                    LastAttendedDate = null,

                    WorkExperienceAwards = allowedSteps.Contains(4) ? false : (bool?)null,
                    WorkExperienceAwardsDate = null,

                    ArticlesReferences = allowedSteps.Contains(5) ? false : (bool?)null,
                    ArticlesReferencesDate = null,

                    PublicationsExperiencesReferences = allowedSteps.Contains(6) ? false : (bool?)null,
                    PublicationsExperiencesReferencesDate = null,

                    EmploymentResearch = allowedSteps.Contains(7) ? false : (bool?)null,
                    EmploymentResearchDate = null,

                    Questionnaire = allowedSteps.Contains(8) ? false : (bool?)null,
                    QuestionnaireDate = null,

                    FinalSubmission = allowedSteps.Contains(9) ? false : (bool?)null,
                    FinalSubmissionDate = null
                };

                db.ApplicationsReports.Add(report);
            }
            else if (report != null)
            {
                // ✅ STEP 3: Update step as completed
                switch (step)
                {
                    case 2:
                        report.AcademicDetails = true;
                        report.AcademicDetailsDate = DateTime.Now;
                        break;
                    case 3:
                        report.LastAttended = true;
                        report.LastAttendedDate = DateTime.Now;
                        break;
                    case 4:
                        report.WorkExperienceAwards = true;
                        report.WorkExperienceAwardsDate = DateTime.Now;
                        break;
                    case 5:
                        report.ArticlesReferences = true;
                        report.ArticlesReferencesDate = DateTime.Now;
                        break;
                    case 6:
                        report.PublicationsExperiencesReferences = true;
                        report.PublicationsExperiencesReferencesDate = DateTime.Now;
                        break;
                    case 7:
                        report.EmploymentResearch = true;
                        report.EmploymentResearchDate = DateTime.Now;
                        break;
                    case 8:
                        report.Questionnaire = true;
                        report.QuestionnaireDate = DateTime.Now;
                        break;
                    case 9:
                        report.FinalSubmission = true;
                        report.FinalSubmissionDate = DateTime.Now;
                        break;
                }
            }

            db.SaveChanges();
        }




        private ActionResult SetLogoForUser()
        {
            if (Session["UserID"] == null)
            {
                // 🔄 If no user is logged in, redirect to LoginForm
                return RedirectToAction("LoginForm", "Form");
            }

            var userId = (int)Session["UserID"];
            var user = db.Users.FirstOrDefault(u => u.UserID == userId);

            string logoFile = "default.jpg"; // fallback

            if (user != null)
            {
                // ✅ First check SchoolID (nullable int)
                if (user.SchoolID != null)
                {
                    var school = db.Schools.FirstOrDefault(s => s.SchoolID == user.SchoolID);
                    if (school != null && !string.IsNullOrEmpty(school.LogoPath))
                    {
                        logoFile = school.LogoPath;
                    }
                }
                else if (user.UniversityID != 0) // ✅ UniversityID is non-nullable
                {
                    var university = db.Universities.FirstOrDefault(u => u.UniversityID == user.UniversityID);
                    if (university != null && !string.IsNullOrEmpty(university.LogoPath))
                    {
                        logoFile = university.LogoPath;
                    }
                }
            }

            // Build the path for the logo in /images/logos/
            ViewBag.LogoPath = Url.Content(logoFile);

            return null; // means no redirect was required
        }



        public ActionResult AdminDasboard()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Form");

            SetLogoForUser();

            int userId = (int)Session["UserID"];
            var user = db.Users.FirstOrDefault(u => u.UserID == userId);

            return View(user); // passes user to Home.cshtml
        }

        public ActionResult ApplicantsReport()
        {
            SetLogoForUser();

            int userId = (int)Session["UserID"];
            var user = db.Users.FirstOrDefault(u => u.UserID == userId);

            if (user == null)
                return RedirectToAction("Login", "Form");

            // Get enabled steps
            List<int> enabledSteps = new List<int>();
            if (user.SchoolID.HasValue)
            {
                var school = db.Schools.FirstOrDefault(s => s.SchoolID == user.SchoolID);
                if (school != null && !string.IsNullOrEmpty(school.Steps))
                {
                    enabledSteps = school.Steps.Split(',').Select(int.Parse).ToList();
                }
            }
            else if (user.UniversityID != 0)
            {
                var university = db.Universities.FirstOrDefault(u => u.UniversityID == user.UniversityID);
                if (university != null && !string.IsNullOrEmpty(university.Steps))
                {
                    enabledSteps = university.Steps.Split(',').Select(int.Parse).ToList();
                }
            }

            // Step Names mapping
            var stepNames = new Dictionary<int, string>
    {
        {1, "Personal Details"},
        {2, "Academic Details"},
        {3, "Last Attended"},
        {4, "Work Experience & Awards"},
        {5, "Articles & References"},
        {6, "Publications & Experiences"},
        {7, "Employment & Research"},
        {8, "Questionnaire"},
        {9, "Final Submission"}
    };

            // Get all applicants reports for this University/School
            var reportsQuery = db.ApplicationsReports.AsQueryable();

            if (user.SchoolID.HasValue)
                reportsQuery = reportsQuery.Where(r => r.SchoolID == user.SchoolID);
            else if (user.UniversityID != 0)
                reportsQuery = reportsQuery.Where(r => r.UniversityID == user.UniversityID);

            var reports = reportsQuery.Include(r => r.Applicant).ToList();

            // Pass data and enabled steps to ViewBag
            ViewBag.EnabledSteps = enabledSteps;
            ViewBag.StepNames = stepNames;

            return View(reports);
        }


        public ActionResult PaymentsReport()
        {
            SetLogoForUser();
            int userId = (int)Session["UserID"];
            var user = db.Users.FirstOrDefault(u => u.UserID == userId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            IQueryable<PaymentReport> reports = Enumerable.Empty<PaymentReport>().AsQueryable();

            if (user.SchoolID.HasValue)
            {
                // Filter by School
                reports = db.PaymentReports.Where(p => p.SchoolID == user.SchoolID);
            }
            else if (user.UniversityID != 0)
            {
                // Filter by University
                reports = db.PaymentReports.Where(p => p.UniversityID == user.UniversityID);
            }

            return View(reports.ToList());
        }

        public ActionResult MyProfile()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "Form");

            SetLogoForUser();

            int userId = (int)Session["UserID"];

            // Step 1: Get User row
            var user = db.Users.FirstOrDefault(u => u.UserID == userId);
            if (user == null)
                return RedirectToAction("Login", "Form");

            // Step 2: Get Admin details
            var admin = db.Admins.FirstOrDefault(a => a.AdminID == user.AdminID);
            if (admin == null)
                return RedirectToAction("Login", "Form");

            // Step 3: Lookup University & School names
            string universityName = null;
            string schoolName = null;

            if (admin.UniversityID.HasValue)
            {
                var uni = db.Universities.FirstOrDefault(u => u.UniversityID == admin.UniversityID);
                if (uni != null)
                    universityName = uni.UniversityName;
            }

            if (admin.SchoolID.HasValue)
            {
                var school = db.Schools.FirstOrDefault(s => s.SchoolID == admin.SchoolID);
                if (school != null)
                    schoolName = school.SchoolName;
            }

            // Step 4: Build ViewModel
            var model = new AdminProfileViewModel
            {
                AdminID = admin.AdminID,
                Name = admin.Name,
                PhoneNumber = admin.PhoneNumber,
                EmailID = admin.EmailID,
                DOB = admin.DOB,
                Role = admin.Role,
                UniversityName = universityName,
                SchoolName = schoolName
            };

            return View(model);
        }

    }
}
