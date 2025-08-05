using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ph.d_Application_Form.Models
{
    public class UniversitySchoolViewModel
    {
        public int UniversityID { get; set; }
        public int? SchoolID { get; set; }

        public List<University> Universities { get; set; }

        // Use string key now
        public Dictionary<string, List<SchoolDto>> SchoolData { get; set; }
    }
}