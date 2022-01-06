using System.Collections.Generic;

namespace SvgToTvgServer.Shared
{
    public class TvgResult
    {
        public List<TvgStep> Steps { get; set; }
        public string Png { get; set; }
    }

    public class TvgStep
    {
        public string StepName { get; set; }
        public string Extension { get; set; }
        public string FileContent { get; set; }
        public bool IsBase64 { get; set; }
    }
}