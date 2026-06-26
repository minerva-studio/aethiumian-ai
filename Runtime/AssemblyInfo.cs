using Aethiumian.AI;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Properties;

[assembly: AssemblyVersion(AssemblyInfo.Version)]
[assembly: AssemblyFileVersion(AssemblyInfo.FileVersion)]
[assembly: InternalsVisibleTo("Aethiumian.AI.Editor")]
[assembly: InternalsVisibleTo("Aethiumian.AI.Editor.Tests")]
[assembly: GeneratePropertyBagsForAssembly]

namespace Aethiumian.AI
{
    public class AssemblyInfo
    {
        /// <summary>
        /// The game revision <br/>
        /// Only need to change when want to publish a same version name but with changes on save/load
        /// </summary> 
        public const string VisionRevision = "1";
        public const string Version = "0.3.2";
        public const string FileVersion = Version + "." + VisionRevision;
    }
}
