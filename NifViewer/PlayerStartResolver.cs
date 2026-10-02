using System;
using System.Collections.Generic;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using OpenSkyrim.NifViewer.Utility;

namespace OpenSkyrim.NifViewer;

/// <summary>
/// Works out where the player is standing when a cell is opened.
/// </summary>
/// <remarks>
/// A cell stores no start position, so the viewpoint is recovered with the fallback chain
/// OpenMW uses for Morrowind (<c>World::findInteriorPosition</c> in
/// <c>apps/openmw/mwworld/worldimp.cpp</c>). Measured over the 590 vanilla interiors,
/// that chain resolves 98.3% of cells from data the game itself stores:
/// "cocmarkerheading" 366 cells (placed exactly once each), an entrance door 208,
/// "xmarkerheading" 6, and nothing 10 - all of the leftovers being utility or test cells.
///
/// "XMarker" is deliberately not consulted. It is a generic debug marker rather than a spawn
/// point, and most interior cells place several of them (37 in Kilkreath Catacombs), so
/// taking the first one lands in an arbitrary room.
/// </remarks>
internal static class PlayerStartResolver
{
	private const string CenterOnCellEditorId = "cocmarkerheading";
	private const string MarkerHeadingEditorId = "xmarkerheading";

	// How far to step into the room from a doorway. A door placement sits on the door frame
	// itself, so starting there puts the camera inside the frame; 100 units is about 1.4
	// metres, which clears the frame without walking far into the room.
	private const float EntranceInset = 100f;

	/// <summary>
	/// Returns where the player stands among <paramref name="placedObjects"/>, and names the
	/// hint it used in <paramref name="source"/>. Returns <c>null</c> when the cell offers
	/// nothing usable, in which case the viewer frames the scene bounds instead.
	/// </summary>
	public static SrtTransform? Resolve(ILinkCache linkCache, IReadOnlyList<IPlacedGetter> placedObjects, out string source)
	{
		source = null;

		if (placedObjects.Count == 0)
		{
			return null;
		}

		// 1. The Creation Toolkit drops a "center on cell" static into a cell to frame it.
		//    It is the only hint carrying an authored heading, and it is placed exactly once
		//    per cell, so it wins whenever present.
		var candidates = new List<SrtTransform>();
		CollectMarkers(linkCache, placedObjects, CenterOnCellEditorId, candidates);

		if (candidates.Count > 0)
		{
			source = CenterOnCellEditorId;
			return candidates[0];
		}

		var center = Centroid(placedObjects);

		// 2. A placed door whose teleport destination lies outside this cell is how the player
		//    gets in. Doors that merely connect two rooms of the same cell point at a sibling
		//    placed object and are skipped.
		candidates.Clear();
		CollectEntranceDoors(linkCache, placedObjects, candidates);

		if (TryNearest(candidates, center, out var entrance))
		{
			// A door stores no meaningful facing: its rotation only lines the door model up
			// with its wall, so using it would regularly point the camera into that wall.
			// The middle of the room is the one direction every reading agrees on.
			var forward = FlatDirection(entrance.Translation, center);
			entrance.Rotation = FacingTowards(entrance.Translation, center);

			// Step past the threshold as well as past the frame, otherwise the camera sits in
			// the doorway itself.
			entrance.Translation += forward * EntranceInset;

			source = "entrance door";
			return entrance;
		}

		// 3. The looser heading marker is the last placed hint. Cells commonly place several
		//    of them, so prefer the one nearest the middle of the cell.
		candidates.Clear();
		CollectMarkers(linkCache, placedObjects, MarkerHeadingEditorId, candidates);

		if (TryNearest(candidates, center, out var heading))
		{
			source = MarkerHeadingEditorId;
			return heading;
		}

		// 4. Nothing usable.
		return null;
	}

	/// <summary>
	/// Collects transforms for placements whose base EditorID matches <paramref name="editorId"/>.
	/// </summary>
	private static void CollectMarkers(ILinkCache linkCache, IReadOnlyList<IPlacedGetter> placedObjects, string editorId, List<SrtTransform> results)
	{
		foreach (var placed in placedObjects)
		{
			if (TryMarkerTransform(linkCache, placed, editorId, out var transform))
			{
				results.Add(transform);
			}
		}
	}

	/// <summary>
	/// Collects transforms for placed doors that teleport out of this cell.
	/// </summary>
	private static void CollectEntranceDoors(ILinkCache linkCache, IReadOnlyList<IPlacedGetter> placedObjects, List<SrtTransform> results)
	{
		// Form keys of everything placed in this cell, which is what tells an outside-leading
		// door apart from one that links two rooms of the same interior.
		var localKeys = new HashSet<uint>();
		foreach (var placed in placedObjects)
		{
			if (placed is IFormKeyGetter identified)
			{
				localKeys.Add(identified.FormKey.ID);
			}
		}

		if (localKeys.Count == 0)
		{
			return;
		}

		foreach (var placed in placedObjects)
		{
			if (placed is not IPlacedObjectGetter placedObject)
			{
				continue;
			}

			var baseLink = placedObject.Base;
			if (baseLink == null || baseLink.IsNull)
			{
				continue;
			}

			// Doors have to be matched as IDoorGetter. The binary overlay implements the getter
			// interface only, so testing for IDoor silently matches nothing at all.
			if (!baseLink.TryResolve(linkCache, out var baseRecord) || baseRecord is not IDoorGetter)
			{
				continue;
			}

			var destination = placedObject.TeleportDestination;
			if (destination?.Door == null || destination.Door.IsNull)
			{
				continue;
			}

			if (localKeys.Contains(destination.Door.FormKey.ID))
			{
				continue;
			}

			if (TryPlacementTransform(placedObject, out var transform))
			{
				results.Add(transform);
			}
		}
	}

	private static bool TryMarkerTransform(ILinkCache linkCache, IPlacedGetter placed, string editorId, out SrtTransform transform)
	{
		transform = default;

		if (placed is not IPlacedObjectGetter placedObject)
		{
			return false;
		}

		var baseLink = placedObject.Base;
		if (baseLink == null || baseLink.IsNull)
		{
			return false;
		}

		if (!baseLink.TryResolve(linkCache, out var baseRecord) || baseRecord == null)
		{
			return false;
		}

		if (!string.Equals(baseRecord.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return TryPlacementTransform(placedObject, out transform);
	}

	private static bool TryPlacementTransform(IPlacedObjectGetter placedObject, out SrtTransform transform)
	{
		transform = default;

		var placement = placedObject.Placement;
		if (placement == null)
		{
			return false;
		}

		// The location root carries no transform, so the player start is in the same space as
		// the props and needs no conversion.
		transform = new SrtTransform(
			placement.Position.ToVector3(),
			PlacementQuaternion(placement),
			placedObject.Scale != null ? new Vector3(placedObject.Scale.Value) : Vector3.One);

		return true;
	}

	/// <summary>
	/// Turns a placement rotation into a quaternion using the same convention the props are
	/// drawn with, so the camera faces exactly where the marker it stands on is facing.
	/// </summary>
	private static Quaternion PlacementQuaternion(IPlacementGetter placement)
	{
		// Matches GetRotation in SkyrimSceneBuilder: the Z sign is flipped and the axes are
		// applied as yaw, pitch, roll. Placements store radians, so going straight to radians
		// here yields the same rotation as the degrees detour the props take.
		var rotation = placement.Rotation;

		return Quaternion.CreateFromYawPitchRoll(rotation.Y, rotation.X, -rotation.Z);
	}

	/// <summary>
	/// Returns the horizontal unit vector pointing from <paramref name="from"/> towards
	/// <paramref name="to"/>, or <see cref="Vector3.Zero"/> when the two share an X/Y position.
	/// </summary>
	private static Vector3 FlatDirection(Vector3 from, Vector3 to)
	{
		var direction = new Vector3(to.X - from.X, to.Y - from.Y, 0f);
		return direction.LengthSquared() <= 0f ? Vector3.Zero : Vector3.Normalize(direction);
	}

	/// <summary>
	/// Builds the rotation that faces local +Y towards <paramref name="to"/>, ignoring height
	/// because placements are Z-up.
	/// </summary>
	private static Quaternion FacingTowards(Vector3 from, Vector3 to)
	{
		var direction = FlatDirection(from, to);
		if (direction == Vector3.Zero)
		{
			return Quaternion.Identity;
		}

		// A heading of t faces (sin t, cos t), which is a rotation of -t about +Z.
		return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -(float)Math.Atan2(direction.X, direction.Y));
	}

	private static Vector3 Centroid(IReadOnlyList<IPlacedGetter> placedObjects)
	{
		var sum = Vector3.Zero;
		var count = 0;

		foreach (var placed in placedObjects)
		{
			var placement = placed.Placement;
			if (placement == null)
			{
				continue;
			}

			sum += placement.Position.ToVector3();
			++count;
		}

		return count == 0 ? Vector3.Zero : sum / count;
	}

	private static bool TryNearest(List<SrtTransform> candidates, Vector3 center, out SrtTransform transform)
	{
		var best = float.MaxValue;
		var found = false;
		transform = default;

		foreach (var candidate in candidates)
		{
			var distance = Vector3.Distance(candidate.Translation, center);
			if (distance >= best)
			{
				continue;
			}

			best = distance;
			transform = candidate;
			found = true;
		}

		return found;
	}
}