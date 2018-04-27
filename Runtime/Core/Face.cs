using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using UnityEngine.Serialization;
using System.Collections.ObjectModel;

namespace UnityEngine.ProBuilder
{
    /// <summary>
    /// A face is composed of a set of triangles, and a material.
    /// </summary>
    [Serializable]
    public class Face
    {
        [FormerlySerializedAs("_indices")]
        [SerializeField]
        int[] m_Indices;

        [FormerlySerializedAs("_distinctIndices")]
        [SerializeField]
        int[] m_DistinctIndices;

        /// <summary>
        /// A cache of the calculated #pb_Edge edges for this face.
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("_edges")]
        Edge[] m_Edges;

        /// <summary>
        /// Adjacent faces sharing this smoothingGroup will have their abutting edge normals averaged.
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("_smoothingGroup")]
        int m_SmoothingGroup;

        /// <summary>
        /// If manualUV is false, these parameters determine how this face's vertices are projected to 2d space.
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("_uv")]
        AutoUnwrapSettings m_Uv;

        /// <summary>
        /// What material does this face use.
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("_mat")]
        Material m_Material;

        /// <summary>
        /// If this face has had it's UV coordinates done by hand, don't update them with the auto unwrap crowd.
        /// </summary>
        public bool manualUV { get; set; }

        /// <summary>
        /// UV element group. Used by the UV editor to group faces.
        /// </summary>
        [SerializeField]
        internal int elementGroup;

        /// <summary>
        /// What texture group this face belongs to. Used when projecting auto UVs.
        /// </summary>
        public int textureGroup { get; set; }

        /// <summary>
        /// Return a reference to the triangle indices that make up this face.
        /// </summary>
        internal int[] indices
        {
            get { return m_Indices; }
	        set
	        {
		        m_Indices = value;
		        InvalidateCache();
	        }
        }

        /// <summary>
        /// The triangle indices that make up this face.
        /// </summary>
        public ReadOnlyCollection<int> indexes
        {
            get { return new ReadOnlyCollection<int>(m_Indices); }
        }

        /// <summary>
        /// Set the triangles that compose this face.
        /// </summary>
        /// <param name="array"></param>
        public void SetIndexes(int[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            int len = array.Length;
            m_Indices = new int[len];
            Array.Copy(array, m_Indices, len);
	        InvalidateCache();
        }

        /// <summary>
        /// Returns a reference to the cached distinct indices (each vertex index is only referenced once in distinctIndices).
        /// </summary>
        internal int[] distinctIndices
        {
            get { return m_DistinctIndices == null ? CacheDistinctIndices() : m_DistinctIndices; }
        }

        /// <summary>
        /// A cached collection of the vertex indices that the indexes array references, made distinct.
        /// </summary>
        public ReadOnlyCollection<int> distinctIndexes
        {
            get { return new ReadOnlyCollection<int>(distinctIndices); }
        }

	    /// <summary>
	    /// A reference to the border edges that make up this face.
	    /// </summary>
	    public Edge[] edgesInternal
	    {
		    get { return m_Edges == null ? CacheEdges() : m_Edges; }
	    }

	    public ReadOnlyCollection<Edge> edges
	    {
		    get { return new ReadOnlyCollection<Edge>(edgesInternal); }
	    }

	    /// <summary>
		/// What smoothing group this face belongs to, if any. This is used to calculate vertex normals.
		/// </summary>
		public int smoothingGroup
		{
			get { return m_SmoothingGroup; }
			set { m_SmoothingGroup = value; }
		}

		/// <summary>
		/// Get the material that face uses.
		/// </summary>
		public Material material
		{
			get { return m_Material; }
			set { m_Material = value; }
		}

		/// <summary>
		/// A reference to the Auto UV mapping parameters.
		/// </summary>
		public AutoUnwrapSettings uv
		{
			get { return m_Uv; }
			set { m_Uv = value; }
		}

		/// <summary>
		/// Accesses the indices array.
		/// </summary>
		/// <param name="i"></param>
		public int this[int i]
		{
			get { return indices[i]; }
		}

		public Face() {}

		public Face(int[] array)
		{
			SetIndexes(array);
			m_Uv = new AutoUnwrapSettings();
			m_Material = BuiltinMaterials.DefaultMaterial;
			m_SmoothingGroup = Smoothing.smoothingGroupNone;
			textureGroup = -1;
			elementGroup = 0;
			m_Edges = null;
			m_DistinctIndices = null;
		}

		[Obsolete]
		public Face(int[] triangles, Material m, AutoUnwrapSettings u, int smoothing, int texture, int element, bool manualUVs)
		{
			SetIndexes(triangles);
			m_Uv = new AutoUnwrapSettings(u);
			m_Material = m;
			m_SmoothingGroup = smoothing;
			textureGroup = texture;
			elementGroup = element;
			manualUV = manualUVs;
		}

		/// <summary>
		/// Deep copy constructor.
		/// </summary>
		/// <param name="other"></param>
		public Face(Face other)
		{
            if (other == null)
                throw new ArgumentNullException("other");
            m_Indices = new int[other.indices.Length];
			System.Array.Copy(other.indices, m_Indices, other.indices.Length);
			m_Uv = new AutoUnwrapSettings(other.uv);
			m_Material = other.material;
			m_SmoothingGroup = other.smoothingGroup;
			textureGroup = other.textureGroup;
			elementGroup = other.elementGroup;
			manualUV = other.manualUV;
			m_Edges = null;
			m_DistinctIndices = null;
		}

		/// <summary>
		/// Copies values from other to this face.
		/// </summary>
		/// <param name="other"></param>
		public void CopyFrom(Face other)
		{
            if (other == null)
                throw new ArgumentNullException("other");
            int len = other.indices == null ? 0 : other.indices.Length;
			m_Indices = new int[len];
			System.Array.Copy(other.indices, m_Indices, len);
			m_SmoothingGroup = other.smoothingGroup;
			m_Uv = new AutoUnwrapSettings(other.uv);
			m_Material = other.material;
			manualUV = other.manualUV;
			elementGroup = other.elementGroup;
		}

		/// <summary>
		/// Check if this face has more than 2 indices.
		/// </summary>
		/// <returns></returns>
		public bool IsValid()
		{
			return indices.Length > 2;
		}

		internal void InvalidateCache()
	    {
		    m_Edges = null;
		    m_DistinctIndices = null;
	    }

		Edge[] CacheEdges()
		{
			if(m_Indices == null)
				return null;

			HashSet<Edge> dist = new HashSet<Edge>();
			List<Edge> dup = new List<Edge>();

			for(int i = 0; i < indices.Length; i+=3)
			{
				Edge a = new Edge(indices[i+0],indices[i+1]);
				Edge b = new Edge(indices[i+1],indices[i+2]);
				Edge c = new Edge(indices[i+2],indices[i+0]);

				if(!dist.Add(a)) dup.Add(a);
				if(!dist.Add(b)) dup.Add(b);
				if(!dist.Add(c)) dup.Add(c);
			}

			dist.ExceptWith(dup);
			m_Edges = dist.ToArray();
			return m_Edges;
		}

		int[] CacheDistinctIndices()
		{
			if(m_Indices == null)
				return null;
			m_DistinctIndices = m_Indices.Distinct().ToArray();
			return distinctIndices;
		}

		/// <summary>
		/// Test if the face contains a triangle.
		/// </summary>
		/// <param name="triangle"></param>
		/// <returns></returns>
		public bool Contains(int[] triangle)
		{
			for(int i = 0; i < indices.Length; i+=3)
			{
				if(	triangle.Contains(indices[i+0]) &&
					triangle.Contains(indices[i+1]) &&
					triangle.Contains(indices[i+2]) )
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns all triangles contained within the #pb_Face array.
		/// </summary>
		/// <param name="q"></param>
		/// <returns></returns>
		internal static int[] AllTriangles(Face[] q)
		{
			List<int> all = new List<int>(q.Length * 6);

			foreach(Face quad in q)
				all.AddRange(quad.indices);

			return all.ToArray();
		}

		/// <summary>
		/// Convert a 2 triangle face to a quad representation.
		/// </summary>
		/// <returns>A quad (4 indices), or null if indices are not able to be represented as a quad.</returns>
		public int[] ToQuad()
		{
            if (indices == null || indices.Length != 6)
                return null;

			int[] quad = new int[4] { edgesInternal[0].x, edgesInternal[0].y, -1, -1 };

			if(edgesInternal[1].x == quad[1])
				quad[2] = edgesInternal[1].y;
			else if(edgesInternal[2].x == quad[1])
				quad[2] = edgesInternal[2].y;
			else if(edgesInternal[3].x == quad[1])
				quad[2] = edgesInternal[3].y;

			if(edgesInternal[1].x == quad[2])
				quad[3] = edgesInternal[1].y;
			else if(edgesInternal[2].x == quad[2])
				quad[3] = edgesInternal[2].y;
			else if(edgesInternal[3].x == quad[2])
				quad[3] = edgesInternal[3].y;

			return quad;
		}

		/// <summary>
		/// Create submeshes from a set of faces. Currently only Quads and Triangles are supported.
		/// </summary>
		/// <param name="faces"></param>
		/// <param name="preferredTopology"></param>
		/// <returns>An array of Submeshes.</returns>
		/// <exception cref="NotImplementedException"></exception>
		public static Submesh[] GetMeshIndices(Face[] faces, MeshTopology preferredTopology = MeshTopology.Triangles)
		{
			if(preferredTopology != MeshTopology.Triangles && preferredTopology != MeshTopology.Quads)
				throw new System.NotImplementedException("Currently only Quads and Triangles are supported.");

            if (faces == null)
                throw new ArgumentNullException("faces");

			bool wantsQuads = preferredTopology == MeshTopology.Quads;

			Dictionary<Material, List<int>> quads = wantsQuads ? new Dictionary<Material, List<int>>() : null;
			Dictionary<Material, List<int>> tris = new Dictionary<Material, List<int>>();

			int count = faces == null ? 0 : faces.Length;

			for(int i = 0; i < count; i++)
			{
				Face face = faces[i];

				if(face.indices == null || face.indices.Length < 1)
					continue;

				Material material = face.material ?? BuiltinMaterials.DefaultMaterial;
				List<int> polys = null;

				int[] res;

				if(wantsQuads && (res = face.ToQuad()) != null)
				{
					if(quads.TryGetValue(material, out polys))
						polys.AddRange(res);
					else
						quads.Add(material, new List<int>(res));
				}
				else
				{
					if(tris.TryGetValue(material, out polys))
						polys.AddRange(face.indices);
					else
						tris.Add(material, new List<int>(face.indices));
				}
			}

			int submeshCount = (quads != null ? quads.Count : 0) + tris.Count;
			var submeshes = new Submesh[submeshCount];
			int ii = 0;

			if(quads != null)
			{
				foreach(var kvp in quads)
					submeshes[ii++] = new Submesh(kvp.Key, MeshTopology.Quads, kvp.Value.ToArray());
			}

			foreach(var kvp in tris)
				submeshes[ii++] = new Submesh(kvp.Key, MeshTopology.Triangles, kvp.Value.ToArray());

			return submeshes;
		}

		public override string ToString()
		{
			// shouldn't ever be the case
			if(indices.Length % 3 != 0)
				return "Index count is not a multiple of 3.";

			System.Text.StringBuilder sb = new System.Text.StringBuilder();

			for(int i = 0; i < indices.Length; i += 3)
			{
				sb.Append("[");
				sb.Append(indices[i]);
				sb.Append(", ");
				sb.Append(indices[i+1]);
				sb.Append(", ");
				sb.Append(indices[i+2]);
				sb.Append("]");

				if(i < indices.Length-3)
					sb.Append(", ");
			}

			return sb.ToString();
		}
	}
}
