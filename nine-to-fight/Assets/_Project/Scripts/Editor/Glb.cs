using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineToFight.EditorTools
{
    /// <summary>
    /// Minimal binary glTF (.glb) reader for the character art: nodes, skins and skinned triangle
    /// meshes with flat-colour materials. Everything comes out in Unity's left-handed space
    /// (glTF's X axis mirrored).
    /// </summary>
    public class Glb
    {
        readonly Dictionary<string, object> json;
        readonly byte[] bin;
        readonly Dictionary<int, int> parents = new Dictionary<int, int>();

        static readonly Matrix4x4 Mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

        public Glb(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67)
                throw new InvalidDataException(path + " is not a binary glTF file");
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            json = (Dictionary<string, object>)MiniJson.Parse(Encoding.UTF8.GetString(bytes, 20, jsonLength));
            int binChunk = 20 + jsonLength;
            int binLength = BitConverter.ToInt32(bytes, binChunk);
            bin = new byte[binLength];
            Buffer.BlockCopy(bytes, binChunk + 8, bin, 0, binLength);

            var nodes = List("nodes");
            for (int i = 0; i < nodes.Count; i++)
                foreach (var child in List(Obj(nodes[i]), "children"))
                    parents[Convert.ToInt32(child)] = i;
        }

        // ------------------------------------------------------------------ json access

        public List<object> List(string key) => List(json, key);

        public static List<object> List(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is List<object> list ? list : new List<object>();

        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;

        public static int Int(Dictionary<string, object> o, string key, int fallback = -1) =>
            o.TryGetValue(key, out var v) ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

        public static string Str(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) ? v as string : null;

        public static float[] Floats(Dictionary<string, object> o, string key, params float[] fallback)
        {
            if (!o.TryGetValue(key, out var v) || !(v is List<object> list)) return fallback;
            var result = new float[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
            return result;
        }

        public Dictionary<string, object> Node(int index) => Obj(List("nodes")[index]);
        public int NodeCount => List("nodes").Count;
        public string NodeName(int index) => Str(Node(index), "name") ?? "Node" + index;
        public int Parent(int index) => parents.TryGetValue(index, out var p) ? p : -1;

        // ------------------------------------------------------------------ transforms

        /// <summary>A node's local transform in glTF space.</summary>
        public Matrix4x4 Local(int index)
        {
            var node = Node(index);
            var m = Floats(node, "matrix");
            if (m != null && m.Length == 16) return Mat4(m, 0);
            var t = Floats(node, "translation", 0f, 0f, 0f);
            var r = Floats(node, "rotation", 0f, 0f, 0f, 1f);
            var s = Floats(node, "scale", 1f, 1f, 1f);
            return Matrix4x4.TRS(new Vector3(t[0], t[1], t[2]), new Quaternion(r[0], r[1], r[2], r[3]), new Vector3(s[0], s[1], s[2]));
        }

        /// <summary>A node's scene-space transform in glTF space.</summary>
        public Matrix4x4 Global(int index)
        {
            var m = Local(index);
            for (int p = Parent(index); p >= 0; p = Parent(p)) m = Local(p) * m;
            return m;
        }

        public static Matrix4x4 Mat4(float[] values, int index)
        {
            var m = new Matrix4x4();
            for (int c = 0; c < 4; c++)
                for (int r = 0; r < 4; r++)
                    m[r, c] = values[index * 16 + c * 4 + r];
            return m;
        }

        /// <summary>glTF (right-handed) to Unity (left-handed): mirror X on both sides.</summary>
        public static Matrix4x4 ToUnity(Matrix4x4 m) => Mirror * m * Mirror;

        public static Vector3 Position(Matrix4x4 m) => m.GetColumn(3);
        public static Quaternion Rotation(Matrix4x4 m) => Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        public static Vector3 Scale(Matrix4x4 m) => new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);

        // ------------------------------------------------------------------ accessors

        public float[] Read(int accessor, out int components)
        {
            var a = Obj(List("accessors")[accessor]);
            var view = Obj(List("bufferViews")[Int(a, "bufferView")]);
            int count = Int(a, "count");
            components = Components(Str(a, "type"));
            int type = Int(a, "componentType");
            bool normalized = a.TryGetValue("normalized", out var n) && n is bool b && b;
            int size = type == 5126 || type == 5125 ? 4 : type == 5123 || type == 5122 ? 2 : 1;
            int stride = Int(view, "byteStride", 0);
            if (stride <= 0) stride = size * components;
            int start = Int(view, "byteOffset", 0) + Int(a, "byteOffset", 0);

            var result = new float[count * components];
            for (int i = 0; i < count; i++)
            {
                for (int c = 0; c < components; c++)
                {
                    int at = start + i * stride + c * size;
                    float v;
                    switch (type)
                    {
                        case 5126: v = BitConverter.ToSingle(bin, at); break;
                        case 5125: v = BitConverter.ToUInt32(bin, at); break;
                        case 5123: v = BitConverter.ToUInt16(bin, at); if (normalized) v /= 65535f; break;
                        case 5122: v = BitConverter.ToInt16(bin, at); if (normalized) v = Mathf.Max(v / 32767f, -1f); break;
                        case 5121: v = bin[at]; if (normalized) v /= 255f; break;
                        case 5120: v = (sbyte)bin[at]; if (normalized) v = Mathf.Max(v / 127f, -1f); break;
                        default: throw new InvalidDataException("Unsupported glTF component type " + type);
                    }
                    result[i * components + c] = v;
                }
            }
            return result;
        }

        static int Components(string type)
        {
            switch (type)
            {
                case "SCALAR": return 1;
                case "VEC2": return 2;
                case "VEC3": return 3;
                case "VEC4": return 4;
                case "MAT4": return 16;
                default: throw new InvalidDataException("Unsupported glTF accessor type " + type);
            }
        }

        /// <summary>Base colour of a material (sRGB) and its name.</summary>
        public (string name, Color color) Material(int index)
        {
            var mat = Obj(List("materials")[index]);
            var pbr = mat.TryGetValue("pbrMetallicRoughness", out var p) ? Obj(p) : new Dictionary<string, object>();
            var f = Floats(pbr, "baseColorFactor", 1f, 1f, 1f, 1f);
            var linear = new Color(f[0], f[1], f[2], f[3]);
            return (Str(mat, "name") ?? "Material" + index, linear.gamma);
        }

        /// <summary>
        /// One primitive of a skinned mesh as a Unity mesh: mirrored positions and normals, flipped
        /// winding, four sorted bone weights per vertex (indices into the node's skin joints).
        /// </summary>
        public Mesh SkinnedPrimitive(Dictionary<string, object> primitive, Matrix4x4[] bindposes, string meshName)
        {
            var attributes = Obj(primitive["attributes"]);
            var positions = Read(Int(attributes, "POSITION"), out _);
            var normals = attributes.ContainsKey("NORMAL") ? Read(Int(attributes, "NORMAL"), out _) : null;
            var joints = Read(Int(attributes, "JOINTS_0"), out _);
            var weights = Read(Int(attributes, "WEIGHTS_0"), out _);
            var indices = Read(Int(primitive, "indices"), out _);

            int count = positions.Length / 3;
            var vertices = new Vector3[count];
            var vertexNormals = new Vector3[count];
            var boneWeights = new BoneWeight[count];
            var influences = new (int bone, float weight)[4];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(-positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
                if (normals != null) vertexNormals[i] = new Vector3(-normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]);
                float total = 0f;
                for (int k = 0; k < 4; k++)
                {
                    influences[k] = ((int)joints[i * 4 + k], weights[i * 4 + k]);
                    total += influences[k].weight;
                }
                Array.Sort(influences, (x, y) => y.weight.CompareTo(x.weight));
                if (total <= 0f) total = 1f;
                boneWeights[i] = new BoneWeight
                {
                    boneIndex0 = influences[0].bone, weight0 = influences[0].weight / total,
                    boneIndex1 = influences[1].bone, weight1 = influences[1].weight / total,
                    boneIndex2 = influences[2].bone, weight2 = influences[2].weight / total,
                    boneIndex3 = influences[3].bone, weight3 = influences[3].weight / total,
                };
            }

            var triangles = new int[indices.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                triangles[i] = (int)indices[i];
                triangles[i + 1] = (int)indices[i + 2];
                triangles[i + 2] = (int)indices[i + 1];
            }

            var mesh = new Mesh { name = meshName };
            if (count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (normals != null) mesh.normals = vertexNormals;
            mesh.triangles = triangles;
            if (normals == null) mesh.RecalculateNormals();
            mesh.boneWeights = boneWeights;
            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ json

        /// <summary>Just enough JSON: objects, arrays, strings, numbers, true/false/null.</summary>
        static class MiniJson
        {
            public static object Parse(string text)
            {
                int i = 0;
                var value = Value(text, ref i);
                return value;
            }

            static void Skip(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{') return Object(s, ref i);
                if (c == '[') return Array(s, ref i);
                if (c == '"') return String(s, ref i);
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static Dictionary<string, object> Object(string s, ref int i)
            {
                var result = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (s[i] == '}') { i++; return result; }
                while (true)
                {
                    Skip(s, ref i);
                    string key = String(s, ref i);
                    Skip(s, ref i);
                    i++; // ':'
                    result[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i++] == '}') return result;
                }
            }

            static List<object> Array(string s, ref int i)
            {
                var result = new List<object>();
                i++;
                Skip(s, ref i);
                if (s[i] == ']') { i++; return result; }
                while (true)
                {
                    result.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i++] == ']') return result;
                }
            }

            static string String(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // opening quote
                while (s[i] != '"')
                {
                    char c = s[i++];
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                i++; // closing quote
                return sb.ToString();
            }
        }
    }
}
