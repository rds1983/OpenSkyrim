using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetManagementBase;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins.Meta;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Noggog;
using Nursia.Materials;
using Nursia.Rendering;
using Nursia.SceneGraph;
using Nursia.SceneGraph.Lights;
using Nursia.Utilities;

namespace OpenSkyrim.NifViewer
{
	public class DrModelViewWidget : Widget
	{
		private readonly ForwardRenderer _renderer = new ForwardRenderer();
		private readonly Scene _scene = new Scene();
		private readonly NursiaModelNode _modelNode = new NursiaModelNode();
		private readonly Camera _camera = new Camera();
		private readonly CameraInputController _cameraController;
		private string _sourcePath;

		public DrModelViewWidget()
		{
			_cameraController = new CameraInputController(_camera)
			{
				MoveSpeed = 2.5f,
				RotationSpeed = 0.15f,
				SprintMultiplier = 2.5f
			};

			var root = new SceneNode();
			root.Children.Add(new DirectLight { Rotation = new Vector3(45, 45, 0), CastsShadow = false });
			root.Children.Add(new DirectLight { Rotation = new Vector3(225, 45, 0), CastsShadow = false });
			root.Children.Add(_modelNode);

			_scene.Root = root;
			_scene.Camera = _camera;
			_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
			_camera.NearPlane = 0.1f;
			_camera.FarPlane = 1000f;
		}

		public string DataDirectory { get; set; }

		public string SourcePath
		{
			get => _sourcePath;
			set => _sourcePath = value;
		}

		public DrModel Model
		{
			get => _modelNode.Model;
			set
			{
				_modelNode.Model = value;
				_modelNode.Materials = BuildMaterials(value);
				ResetCamera();
			}
		}

		private IMaterial[][] BuildMaterials(DrModel model)
		{
			if (model == null)
			{
				return null;
			}

			var materials = new IMaterial[model.Meshes.Length][];
			for (var meshIndex = 0; meshIndex < model.Meshes.Length; ++meshIndex)
			{
				var mesh = model.Meshes[meshIndex];
				materials[meshIndex] = new IMaterial[mesh.MeshParts.Count];

				for (var partIndex = 0; partIndex < mesh.MeshParts.Count; ++partIndex)
				{
					var texturePath = ResolveTexturePath(mesh.Name);
					var material = new UnlitMaterial
					{
						DiffuseColor = Color.White,
						Texture = TryLoadTexture(texturePath)
					};

					materials[meshIndex][partIndex] = material;
				}
			}

			return materials;
		}

		private Texture2D TryLoadTexture(string texturePath)
		{
			if (string.IsNullOrWhiteSpace(texturePath))
			{
				return null;
			}

			try
			{
				if (texturePath.StartsWith("bsa://", StringComparison.OrdinalIgnoreCase))
				{
					var parts = texturePath.Substring("bsa://".Length).Split(new[] { "|" }, 2, StringSplitOptions.None);
					if (parts.Length == 2)
					{
						var archivePath = parts[0];
						var assetName = parts[1].Replace('\\', '/').TrimStart('/');
						var bsaAssetManager = new AssetManager(new BsaAssetResolver(archivePath), DataDirectory ?? string.Empty);
						return (Texture2D)bsaAssetManager.LoadTexture(MyraEnvironment.GraphicsDevice, assetName);
					}
				}

				var resolvedPath = texturePath;
				if (!Path.IsPathRooted(resolvedPath))
				{
					resolvedPath = Path.Combine(DataDirectory ?? string.Empty, resolvedPath.Replace('/', '\\'));
				}

				if (!File.Exists(resolvedPath))
				{
					return null;
				}

				var assetManager = AssetManager.CreateFileAssetManager(Path.GetDirectoryName(resolvedPath)!);
				return (Texture2D)assetManager.LoadTexture(MyraEnvironment.GraphicsDevice, $"@{resolvedPath}");
			}
			catch
			{
				return null;
			}
		}

		private string ResolveTexturePath(string meshName)
		{
			var loosePath = ResolveLooseTexturePath(meshName);
			if (!string.IsNullOrWhiteSpace(loosePath))
			{
				return loosePath;
			}

			return ResolveArchiveTexturePath(meshName);
		}

		private string ResolveLooseTexturePath(string meshName)
		{
			if (string.IsNullOrWhiteSpace(DataDirectory) || !Directory.Exists(DataDirectory))
			{
				return null;
			}

			var texturesRoot = Path.Combine(DataDirectory, "Textures");
			if (!Directory.Exists(texturesRoot))
			{
				return null;
			}

			var candidateNames = GetTextureCandidates(meshName);
			foreach (var candidate in candidateNames)
			{
				var matches = Directory.EnumerateFiles(texturesRoot, "*.*", SearchOption.AllDirectories)
					.Where(file => file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
					.Where(file =>
					{
						var stem = Path.GetFileNameWithoutExtension(file);
						return string.Equals(stem, candidate, StringComparison.OrdinalIgnoreCase)
							|| string.Equals(stem, candidate + "_d", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(stem, candidate + "_diff", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(stem, candidate + "_color", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(stem, candidate + "_albedo", StringComparison.OrdinalIgnoreCase);
					})
					.ToArray();

				if (matches.Length > 0)
				{
					return matches[0];
				}
			}

			return null;
		}

		private string ResolveArchiveTexturePath(string meshName)
		{
			if (string.IsNullOrWhiteSpace(DataDirectory) || !Directory.Exists(DataDirectory))
			{
				return null;
			}

			var candidateNames = GetTextureCandidates(meshName);
			foreach (var bsaFile in Directory.EnumerateFiles(DataDirectory, "*.bsa", SearchOption.AllDirectories))
			{
				try
				{
					var archive = Archive.CreateReader(GameConstants.SkyrimSE.Release, new FilePath(bsaFile));
					foreach (var archiveFile in archive.Files)
					{
						if (!string.Equals(Path.GetExtension(archiveFile.Path), ".dds", StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						var archiveStem = Path.GetFileNameWithoutExtension(archiveFile.Path);
						if (!candidateNames.Any(candidate =>
							string.Equals(archiveStem, candidate, StringComparison.OrdinalIgnoreCase)
							|| string.Equals(archiveStem, candidate + "_d", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(archiveStem, candidate + "_diff", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(archiveStem, candidate + "_color", StringComparison.OrdinalIgnoreCase)
							|| string.Equals(archiveStem, candidate + "_albedo", StringComparison.OrdinalIgnoreCase)))
						{
							continue;
						}

						return $"bsa://{bsaFile}|{archiveFile.Path.Replace('\\', '/')}";
					}
				}
				catch
				{
					// Ignore invalid or unreadable archives and keep searching.
				}
			}

			return null;
		}

		private List<string> GetTextureCandidates(string meshName)
		{
			var candidateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var sourceStem = Path.GetFileNameWithoutExtension(_sourcePath ?? string.Empty);
			if (!string.IsNullOrWhiteSpace(sourceStem))
			{
				candidateNames.Add(sourceStem);
				candidateNames.Add(sourceStem.Replace("_nif", string.Empty, StringComparison.OrdinalIgnoreCase));
			}

			if (!string.IsNullOrWhiteSpace(meshName))
			{
				candidateNames.Add(meshName);
				candidateNames.Add(meshName.Replace("_nif", string.Empty, StringComparison.OrdinalIgnoreCase));
				candidateNames.Add(meshName + "_d");
				candidateNames.Add(meshName + "_diff");
				candidateNames.Add(meshName + "_color");
				candidateNames.Add(meshName + "_albedo");
			}

			return candidateNames
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.Select(n => n.Trim())
				.ToList();
		}

		public void UpdateCameraInput()
		{
			if (_modelNode.Model != null)
			{
				_cameraController.Update();
			}
		}

		public override void InternalRender(RenderContext context)
		{
			if (_modelNode.Model == null || ActualBounds.Width <= 0 || ActualBounds.Height <= 0)
			{
				return;
			}

			context.End();

			var device = MyraEnvironment.GraphicsDevice;
			var previousViewport = device.Viewport;

			var bounds = context.ToGlobal(ActualBounds);
			device.Viewport = new Viewport(bounds.X, bounds.Y, bounds.Width, bounds.Height);
			_scene.Render(_renderer, _camera);
			device.Viewport = previousViewport;

			context.Begin();
		}

		private void ResetCamera()
		{
			if (_modelNode.Model == null)
			{
				_camera.View = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
				return;
			}

			var boundingBox = _modelNode.BoundingBox;
			if (!boundingBox.HasValue)
			{
				return;
			}

			var min = boundingBox.Value.Min;
			var max = boundingBox.Value.Max;
			var center = (min + max) / 2f;
			var size = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
			var distance = Math.Max(size * 1.75f, 5f);

			_camera.View = Matrix.CreateLookAt(new Vector3(center.X, center.Y, center.Z + distance), center, Vector3.Up);
			_camera.NearPlane = Math.Max(0.01f, size / 1000f);
			_camera.FarPlane = Math.Max(1000f, size * 20f);
		}
	}
}
