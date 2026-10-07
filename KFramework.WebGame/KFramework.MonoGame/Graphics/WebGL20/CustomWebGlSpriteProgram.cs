using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 自定义精灵着色器程序（WebGL 2.0 / GLSL ES 3.00）。
    /// <para>
    /// 顶点着色器复用标准精灵顶点格式（aPosition / aTexCoord / aColor + uProjection），
    /// 顶点属性位置与默认 <see cref="SpriteEffect"/> 一致（0/1/2），因此可直接套用后端已配置好的 VAO。
    /// 片元着色器由外部传入，并额外暴露 <c>uTime</c>（动画时间）与 <c>uParams</c>（vec4，可携带分辨率/参数）两个 uniform。
    /// </para>
    /// <para>配合 <see cref="ShaderEffect"/> 使用：场景每帧写入 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>，Apply 时随材质下发。</para>
    /// </summary>
    internal sealed class CustomWebGlSpriteProgram : ISpriteProgram, ICustomSpriteProgram
    {
        private const string VertexSource = @"#version 300 es
in vec2 aPosition;
in vec2 aTexCoord;
in vec4 aColor;
in vec4 aParams0;
in vec4 aParams1;
uniform mat4 uProjection;
out vec2 vTexCoord;
out vec4 vColor;
out vec4 vParams0;
out vec4 vParams1;
void main()
{
    gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
    vTexCoord = aTexCoord;
    vColor = aColor;
    vParams0 = aParams0;
    vParams1 = aParams1;
}";

        private readonly JSObject _program;
        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly JSObject? _timeLocation;
        private readonly JSObject? _paramsLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        /// <summary>材质纹理绑定的纹理单元：0 号留给 SpriteBatch 的精灵纹理，材质纹理从 1 号开始。</summary>
        private const int MaterialTextureUnit = 1;

        /// <summary>属性名 → uniform location 的缓存（照 Unity 缓存 Shader.PropertyToID 的思路，避免每帧跨界查询）。
        /// 值为 null 表示着色器里没有这个 uniform（也缓存下来，免得每帧重查）。</summary>
        private readonly Dictionary<string, JSObject?> _propertyLocations = new(StringComparer.Ordinal);

        private ShaderEffect? _owner;

        internal CustomWebGlSpriteProgram(string fragmentSource)
        {
            _program = JSBind_WEBGL20.CreateProgram();

            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER, fragmentSource);

            JSBind_WEBGL20.AttachShader(_program, vertexShader);
            JSBind_WEBGL20.AttachShader(_program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(_program);

            if (JSBind_WEBGL20.GetProgramParameter(_program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("自定义着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(_program));

            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTexture");
            _timeLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTime");
            _paramsLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uParams");
        }

        private static JSObject Compile(int type, string source)
        {
            JSObject shader = JSBind_WEBGL20.CreateShader(type);
            JSBind_WEBGL20.ShaderSource(shader, source);
            JSBind_WEBGL20.CompileShader(shader);
            if (JSBind_WEBGL20.GetShaderParameter(shader, JSBind_WEBGL20.COMPILE_STATUS) == 0)
            {
                string log = JSBind_WEBGL20.GetShaderInfoLog(shader);
                JSBind_WEBGL20.DeleteShader(shader);
                throw new InvalidOperationException($"自定义着色器编译失败: {log}");
            }
            return shader;
        }

        void ICustomSpriteProgram.SetOwner(ShaderEffect owner) => _owner = owner;

        public bool IsAnimated => true;

        public void Apply(Matrix4x4 projection, Material material, MaterialPropertyBlock? properties)
        {
            JSBind_WEBGL20.UseProgram(_program);
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            if (_textureLocation is not null) JSBind_WEBGL20.Uniform1i(_textureLocation, 0);

            // 旧路径（保持兼容）：uTime / uParams 从 ShaderEffect.Time / Params 取。
            // 材质或属性块里显式设了同名属性时以它们为准 —— 交给下面的 ApplyProperties 覆盖，这里就不发。
            if (_timeLocation is not null && !HasProperty(material, properties, "uTime"))
                JSBind_WEBGL20.Uniform1f(_timeLocation, _owner?.Time ?? 0f);
            if (_paramsLocation is not null && !HasProperty(material, properties, "uParams"))
            {
                Vector4 p = _owner?.Params ?? Vector4.Zero;
                JSBind_WEBGL20.Uniform4f(_paramsLocation, p.X, p.Y, p.Z, p.W);
            }

            // 先材质基线，再属性块覆盖（照 Unity：MaterialPropertyBlock 的值盖在材质之上）。
            ApplyProperties(material, material.Sampler);
            if (properties is not null) ApplyProperties(properties, material.Sampler);
        }

        /// <summary>材质或属性块里是否设了该属性（用于判断内置 uTime / uParams 要不要走旧路径）。</summary>
        private static bool HasProperty(Material material, MaterialPropertyBlock? properties, string name)
        {
            return material.HasProperty(name) || (properties is not null && properties.HasProperty(name));
        }

        /// <summary>
        /// 把属性表（材质或属性块）里的着色器属性逐个灌入本程序的 uniform（按属性类型选接口：1f / 1i / 4f / Matrix4fv / 纹理单元）。
        /// 属性名在本着色器里不存在就跳过（照 Unity：设了没用到的属性不报错也不生效）。
        /// </summary>
        private void ApplyProperties(MaterialProperties source, SamplerState sampler)
        {
            IReadOnlyDictionary<string, MaterialProperty> properties = source.Properties;
            if (properties.Count == 0) return;

            foreach (KeyValuePair<string, MaterialProperty> pair in properties)
            {
                JSObject? location = PropertyLocation(pair.Key);
                if (location is null) continue;

                MaterialProperty property = pair.Value;
                switch (property.Type)
                {
                    case MaterialPropertyType.Int:
                        JSBind_WEBGL20.Uniform1i(location, property.Int);
                        break;
                    case MaterialPropertyType.Vector:
                    case MaterialPropertyType.Color:
                        JSBind_WEBGL20.Uniform4f(location, property.Vector.X, property.Vector.Y, property.Vector.Z, property.Vector.W);
                        break;
                    case MaterialPropertyType.Matrix:
                        WriteMatrix(property.Matrix, _matrixBuffer);
                        JSBind_WEBGL20.UniformMatrix4fv(location, 0, _matrixBuffer);
                        break;
                    case MaterialPropertyType.Texture:
                        BindMaterialTexture(location, sampler, property.Texture);
                        break;
                    default:
                        JSBind_WEBGL20.Uniform1f(location, property.Float);
                        break;
                }
            }
        }

        /// <summary>取属性的 uniform location（首次查询后缓存；着色器里没有则缓存 null 并一直跳过）。</summary>
        private JSObject? PropertyLocation(string name)
        {
            if (_propertyLocations.TryGetValue(name, out JSObject? cached)) return cached;
            JSObject? location = JSBind_WEBGL20.GetUniformLocation(_program, name);
            _propertyLocations[name] = location;
            return location;
        }

        /// <summary>
        /// 把材质纹理绑到 1 号纹理单元并把单元号写进 sampler uniform（0 号留给 SpriteBatch 的精灵纹理）；
        /// 采样参数写在纹理对象上（WebGL2 无独立 sampler 对象），同一张纹理 + 同一采样器只下发一次。
        /// </summary>
        private void BindMaterialTexture(JSObject location, SamplerState sampler, Texture2D? texture)
        {
            JSBind_WEBGL20.ActiveTexture(JSBind_WEBGL20.TEXTURE0 + MaterialTextureUnit);
            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, texture?.Handle ?? 0);

            if (texture is not null && !ReferenceEquals(texture._appliedSampler, sampler))
            {
                JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MIN_FILTER, WebGl20Backend.ToGLFilter(sampler.MinFilter));
                JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MAG_FILTER, WebGl20Backend.ToGLFilter(sampler.MagFilter));
                JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_S, WebGl20Backend.ToGLAddressMode(sampler.WrapMode));
                JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_T, WebGl20Backend.ToGLAddressMode(sampler.WrapMode));
                texture._appliedSampler = sampler;
            }

            JSBind_WEBGL20.Uniform1i(location, MaterialTextureUnit);
            // 复位到 0 号单元：后续 SpriteBatch 绑定精灵纹理默认走 0 号，避免把材质纹理的绑定串过去。
            JSBind_WEBGL20.ActiveTexture(JSBind_WEBGL20.TEXTURE0);
        }

        public void Dispose() => JSBind_WEBGL20.DeleteProgram(_program);

        /// <summary>
        /// 把矩阵写成 16 个 float 的小端字节流交给 WebGL（列主序 + 转置抵消，见 <see cref="SpriteEffect"/> 说明）。
        /// </summary>
        private static void WriteMatrix(in Matrix4x4 value, Span<byte> destination)
        {
            Write(destination, 0, value.M11); Write(destination, 1, value.M12);
            Write(destination, 2, value.M13); Write(destination, 3, value.M14);
            Write(destination, 4, value.M21); Write(destination, 5, value.M22);
            Write(destination, 6, value.M23); Write(destination, 7, value.M24);
            Write(destination, 8, value.M31); Write(destination, 9, value.M32);
            Write(destination, 10, value.M33); Write(destination, 11, value.M34);
            Write(destination, 12, value.M41); Write(destination, 13, value.M42);
            Write(destination, 14, value.M43); Write(destination, 15, value.M44);
        }

        private static void Write(Span<byte> destination, int index, float value)
            => BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);
    }
}
