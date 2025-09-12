using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ph.d_Application_Form.Models
{
    public class DashboardViewModel
    {
        public int ApplicantID { get; set; }
        public string Name { get; set; }
        public string EmailID { get; set; }
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
        public string PhotoPath { get; set; }
        public List<int> VisibleSteps { get; set; }  // 👈 Added
        public ApplicationFormHeaderViewModel Dashboard { get; set; }
    }

}