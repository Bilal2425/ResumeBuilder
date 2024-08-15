using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseApi.Model
{
    public class WorkExperience
    {
        public string? Company { get; set; }
        public string? Position { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        // Add other work experience fields as needed
    }
}
