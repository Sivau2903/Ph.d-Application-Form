using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ph.d_Application_Form.Models
{
    public class AdminProfileViewModel
    {
        public int AdminID { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string EmailID { get; set; }
        public DateTime? DOB { get; set; }
        public string Role { get; set; }

        public string UniversityName { get; set; }
        public string SchoolName { get; set; }
    }

}