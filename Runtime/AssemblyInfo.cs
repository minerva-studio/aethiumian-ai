using Amlos.AI;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Properties;

[assembly: AssemblyVersion(AssemblyInfo.Version)]
[assembly: AssemblyFileVersion(AssemblyInfo.FileVersion)]
[assembly: InternalsVisibleTo("Aethiumian.Editor")]
[assembly: InternalsVisibleTo("Aethiumian.Tests")]
[assembly: GeneratePropertyBagsForAssembly]

namespace Amlos.AI
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
