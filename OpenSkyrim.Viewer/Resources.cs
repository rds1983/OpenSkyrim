using AssetManagementBase;
using Nursia;
using Nursia.SceneGraph;

namespace OpenSkyrim.Viewer;

internal static class Resources
{
	private static readonly AssetManager _assetManager = AssetManager.CreateResourceAssetManager(typeof(Resources).Assembly, "OpenSkyrim.Viewer.Assets", false);
	private static SceneNode _modelAxises;

	public static SceneNode ModelAxises
	{
		get
		{
			if (_modelAxises == null)
			{
				_modelAxises = _assetManager.LoadSceneNode("Scenes/axises.scene");
			}

			return _modelAxises;
		}
	}
}
