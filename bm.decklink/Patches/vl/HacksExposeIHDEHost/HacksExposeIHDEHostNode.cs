#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "ExposeIHDEHost", Category = "Hacks")]
	#endregion PluginInfo
	public class HacksExposeIHDEHostNode : IPluginEvaluate
	{
		[Output("Output")]
		public ISpread<IHDEHost> FOutput;

		[Import]
        private IHDEHost FHDEHost;

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FOutput[0] = FHDEHost;
		}
	}
}
