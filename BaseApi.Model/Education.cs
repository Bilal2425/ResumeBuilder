using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseApi.Model
{
    public class Education
    {
        public string? Institution { get; set; }
        public string? Degree { get; set; }
        public DateTime? GraduationDate { get; set; }
        // Add other education fields as needed
    }
}
