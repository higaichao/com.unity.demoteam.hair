using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairInstanceBuilder
	{
		public static void ClearHairInstance(HairInstance hairInstance)
		{
			if (hairInstance.strandGroupInstances != null)
			{
				foreach (var strandGroupInstance in hairInstance.strandGroupInstances)
				{
					CoreUtils.Destroy(strandGroupInstance.container);
					CoreUtils.Destroy(strandGroupInstance.materialInstance);
					CoreUtils.Destroy(strandGroupInstance.meshInstanceLines);
					CoreUtils.Destroy(strandGroupInstance.meshInstanceStrips);
				}

				hairInstance.strandGroupInstances = null;
				hairInstance.strandGroupInstancesChecksum = string.Empty;
			}

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}

		public static void BuildHairInstance(HairInstance hairInstance, HairAsset hairAsset, HideFlags hideFlags = HideFlags.NotEditable)
		{
			ClearHairInstance(hairInstance);

			var strandGroups = hairAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return;

			// prep strand group instances
			hairInstance.strandGroupInstances = new HairInstance.StrandGroupInstance[strandGroups.Length];

			// build strand group instances
			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var strandGroupInstance = ref hairInstance.strandGroupInstances[i];

				strandGroupInstance.container = CreateContainer("Group:" + i, hairInstance.gameObject, hideFlags);

				// create scene objects for roots
				strandGroupInstance.rootContainer = CreateContainer("Roots:" + i, strandGroupInstance.container, hideFlags);
				{
					strandGroupInstance.rootFilter = CreateComponent<MeshFilter>(strandGroupInstance.rootContainer, hideFlags);
					strandGroupInstance.rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
					strandGroupInstance.rootAttachment = CreateComponent<SkinAttachment>(strandGroupInstance.rootContainer, hideFlags);
					strandGroupInstance.rootAttachment.attachmentType = SkinAttachment.AttachmentType.Mesh;
					strandGroupInstance.rootAttachment.forceRecalculateBounds = true;
#endif
				}

				// create scene objects for strands
				strandGroupInstance.strandContainer = CreateContainer("Strands:" + i, strandGroupInstance.container, hideFlags);
				{
					strandGroupInstance.strandFilter = CreateComponent<MeshFilter>(strandGroupInstance.strandContainer, hideFlags);
					strandGroupInstance.strandRenderer = CreateComponent<MeshRenderer>(strandGroupInstance.strandContainer, hideFlags);
				}
			}

			hairInstance.strandGroupInstancesChecksum = hairAsset.checksum;

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}

		//--------
		// meshes

		public static unsafe void BuildMeshRoots(Mesh meshRoots, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			using (var indices = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write indices
				for (int i = 0; i != strandCount; i++)
				{
					*(indicesPtr++) = i;
				}

				// apply to mesh
				var meshVertexCount = strandCount;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshRoots.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0),
						new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1)
					);

					meshRoots.SetVertexBufferData(rootPosition, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshRoots.SetVertexBufferData(rootDirection, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshRoots.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshRoots.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshRoots.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Points), meshUpdateFlags);
					meshRoots.RecalculateBounds();
				}
			}
		}

		public static unsafe void BuildMeshLines(Mesh meshLines, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perLineVertices = strandParticleCount;
			var perLineSegments = perLineVertices - 1;
			var perLineIndices = perLineSegments * 2;

			var unormU0 = (uint)(UInt16.MaxValue * 0.5f);
			var unormVk = UInt16.MaxValue / (float)perLineSegments;

			using (var vertexID = new NativeArray<float>(strandCount * perLineVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var vertexUV = new NativeArray<uint>(strandCount * perLineVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var indices = new NativeArray<int>(strandCount * perLineIndices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var vertexIDPtr = (float*)vertexID.GetUnsafePtr();
				var vertexUVPtr = (uint*)vertexUV.GetUnsafePtr();
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write vertex ID
				for (int i = 0, k = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						*(vertexIDPtr++) = k++;// vertexID
					}
				}

				// write vertex UV
				for (int i = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin, k = 0; j != strandParticleEnd; j += strandParticleStride, k++)
					{
						var unormV = (uint)(unormVk * k);
						{
							*(vertexUVPtr++) = (unormV << 16) | unormU0;// texCoord
						}
					}
				}

				// write indices
				for (int i = 0, segmentBase = 0; i != strandCount; i++, segmentBase++)
				{
					for (int j = 0; j != perLineSegments; j++, segmentBase++)
					{
						*(indicesPtr++) = segmentBase;
						*(indicesPtr++) = segmentBase + 1;
					}
				}

				// apply to mesh
				var meshVertexCount = strandCount * perLineVertices;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshLines.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
						new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
					);

					meshLines.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshLines.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshLines.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshLines.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshLines.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Lines), meshUpdateFlags);
					meshLines.bounds = bounds;
				}
			}
		}

		public static unsafe void BuildMeshStrips(Mesh meshStrips, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perStripVertices = 2 * strandParticleCount;
			var perStripSegments = strandParticleCount - 1;
			var perStripTriangles = 2 * perStripSegments;
			var perStripsIndices = perStripTriangles * 3;

			var unormU0 = (uint)(UInt16.MaxValue * 0.0f);
			var unormU1 = (uint)(UInt16.MaxValue * 1.0f);
			var unormVs = UInt16.MaxValue / (float)perStripSegments;

			using (var vertexID = new NativeArray<float>(strandCount * perStripVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var vertexUV = new NativeArray<uint>(strandCount * perStripVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var indices = new NativeArray<int>(strandCount * perStripsIndices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var vertexIDPtr = (float*)vertexID.GetUnsafePtr();
				var vertexUVPtr = (uint*)vertexUV.GetUnsafePtr();
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write vertex ID
				for (int i = 0, k = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						// two vertices per particle
						*(vertexIDPtr++) = k++;// vertexID
						*(vertexIDPtr++) = k++;// ...
					}
				}

				// write vertex UV
				for (int i = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin, k = 0; j != strandParticleEnd; j += strandParticleStride, k++)
					{
						var unormV = (uint)(unormVs * k);
						{
							// two vertices per particle
							*(vertexUVPtr++) = (unormV << 16) | unormU0;// texCoord
							*(vertexUVPtr++) = (unormV << 16) | unormU1;// ...
						}
					}
				}

				// write indices
				for (int i = 0, segmentBase = 0; i != strandCount; i++, segmentBase += 2)
				{
					for (int j = 0; j != perStripSegments; j++, segmentBase += 2)
					{
						//  :  .   :
						//  |,     |
						//  4------5
						//  |    ,�|
						//  |  ,�  |      etc.
						//  |,�    |    
						//  2------3    12----13
						//  |    ,�|    |    ,�|
						//  |  ,�  |    |  ,�  |
						//  |,�    |    |,�    |
						//  0------1    10----11
						//  .
						//  |
						//  '--- segmentBase

						// indices for first triangle
						*(indicesPtr++) = segmentBase + 0;
						*(indicesPtr++) = segmentBase + 1;
						*(indicesPtr++) = segmentBase + 3;

						// indices for second triangle
						*(indicesPtr++) = segmentBase + 0;
						*(indicesPtr++) = segmentBase + 3;
						*(indicesPtr++) = segmentBase + 2;
					}
				}

				// apply to mesh asset
				var meshVertexCount = strandCount * perStripVertices;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshStrips.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
						new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
					);

					meshStrips.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshStrips.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshStrips.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshStrips.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshStrips.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles), meshUpdateFlags);
					meshStrips.bounds = bounds;
				}
			}
		}

		public static Mesh CreateMeshRoots(HideFlags hideFlags, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			var meshRoots = new Mesh();
			{
				meshRoots.hideFlags = hideFlags;
				meshRoots.name = "Roots";
				BuildMeshRoots(meshRoots, strandCount, rootPosition, rootDirection);
			}
			return meshRoots;
		}

		public static void CreateMeshRootsIfNull(ref Mesh meshRoots, HideFlags hideFlags, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			if (meshRoots == null)
				meshRoots = CreateMeshRoots(hideFlags, strandCount, rootPosition, rootDirection);
		}

		public static Mesh CreateMeshLines(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshLines = new Mesh();
			{
				meshLines.hideFlags = hideFlags;
				meshLines.name = "X-Lines";
				BuildMeshLines(meshLines, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshLines;
		}

		public static void CreateMeshLinesIfNull(ref Mesh meshLines, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshLines == null)
				meshLines = CreateMeshLines(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);
		}

		public static Mesh CreateMeshStrips(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshStrips = new Mesh();
			{
				meshStrips.hideFlags = hideFlags;
				meshStrips.name = "X-Strips";
				BuildMeshStrips(meshStrips, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshStrips;
		}

		public static void CreateMeshStripsIfNull(ref Mesh meshStrips, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshStrips == null)
				meshStrips = CreateMeshStrips(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);
		}

		public static Mesh CreateMeshInstance(Mesh original, HideFlags hideFlags)
		{
			var instance = Mesh.Instantiate(original);
			{
				instance.name = original.name + "(Instance)";
				instance.hideFlags = hideFlags;
			}
			return instance;
		}

		public static void CreateMeshInstanceIfNull(ref Mesh instance, Mesh original, HideFlags hideFlags)
		{
			if (instance == null)
				instance = CreateMeshInstance(original, hideFlags);
		}

		//------------
		// containers

		public static GameObject CreateContainer(string name, GameObject parentContainer, HideFlags hideFlags)
		{
			var container = new GameObject(name);
			{
				container.transform.SetParent(parentContainer.transform, worldPositionStays: false);
				container.hideFlags = hideFlags;
			}
			return container;
		}

		public static T CreateComponent<T>(GameObject container, HideFlags hideFlags) where T : Component
		{
			var component = container.AddComponent<T>();
			{
				component.hideFlags = hideFlags;
			}
			return component;
		}

		public static void CreateComponentIfNull<T>(ref T component, GameObject container, HideFlags hideFlags) where T : Component
		{
			if (component == null)
				component = CreateComponent<T>(container, hideFlags);
		}
	}
}