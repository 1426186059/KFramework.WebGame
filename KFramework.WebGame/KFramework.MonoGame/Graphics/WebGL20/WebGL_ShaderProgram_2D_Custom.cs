using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// WebGL 2D 的自定义着色器程序（GLSL ES 3.00）：片元着色器由外部传入。
    /// <para>
    /// 顶点着色器复用标准精灵顶点格式（aPosition / aColor / aTexCoord + uProjection），
    /// 声明顺序与默认 <see cref="WebGL_ShaderProgram_2D_Default"/> 完全一致，因此可直接套用后端已配置好的 VAO。
    /// 片元着色器里任意 uniform 都能被设置：把属性名写进 <c>material.Effect</c>（默认材质属性）或
    /// <see cref="ShaderPropertyBlock"/>（这一次绘制的覆盖值）即可，绘制时按这个先后顺序灌 uniform
    /// （约定名 <c>uTime</c> / <c>uParams</c> 只是常用写法，引擎不做特殊处理）。
    /// </para>
    /// </summary>
    internal sealed class WebGL_ShaderProgram_2D_Custom : IShaderProgram
    {
        private const string VertexSource = @"#version 300 es
// 顶点输入对齐 Unity 精灵着色器的 appdata_t：aPosition ↔ float4 vertex : POSITION，
// aColor ↔ float4 color : COLOR，aTexCoord ↔ float2 texcoord : TEXCOORD0。
// 声明顺序必须与 WebGl20Backend.ConfigureAttributes 的槽位顺序一致（位置 → 颜色 → UV），
// 否则链接期分配到的属性位置会与后端配置好的 VAO 槽位错开。
in vec4 aPosition;
in vec4 aColor;
in vec2 aTexCoord;
uniform mat4 uProjection;
out vec2 vTexCoord;
out vec4 vColor;
// UNITY_VERTEX_INPUT_INSTANCE_ID 在 GLSL 里的等价物：实例号是内置输入，不占顶点布局。
flat out int vInstanceID;
void main()
{
    gl_Position = uProjection * aPosition;
    vTexCoord = aTexCoord;
    vColor = aColor;
    vInstanceID = gl_InstanceID;
}";

        private readonly JSObject _program;
        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        /// <summary>材质纹理绑定的纹理单元：0 号留给 SpriteBatch 的精灵纹理，材质纹理从 1 号开始。</summary>
        private const int MaterialTextureUnit = 1;

        /// <summary>属性名 → uniform location 的缓存（照 Unity 缓存 Shader.PropertyToID 的思路，避免每帧跨界查询）。
        /// 值为 null 表示着色器里没有这个 uniform（也缓存下来，免得每帧重查）。</summary>
        private readonly Dictionary<string, JSObject?> _propertyLocations = new(StringComparer.Ordinal);

        internal WebGL_ShaderProgram_2D_Custom(string fragmentSource)
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

        public bool IsAnimated => true;

        public void Apply(Matrix4x4 projection, Material material, ShaderPropertyBlock? block)
        {
            JSBind_WEBGL20.UseProgram(_program);
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            if (_textureLocation is not null) JSBind_WEBGL20.Uniform1i(_textureLocation, 0);

            // 先发效果上的默认材质属性（SpriteBatch.Begin 已把空效果落到 ShaderEffect.Default），
            // 再发这一次绘制的覆盖块：同名 uniform 后写的赢（照 Unity 的 SetPropertyBlock）。
            ApplyProperties(material.Effect!, material.Sampler);
            if (block is not null) ApplyProperties(block, material.Sampler);
        }

        /// <summary>
        /// 把属性表里的着色器属性逐个灌入本程序的 uniform（按属性类型选接口：1f / 1i / 4f / Matrix4fv / 纹理单元）。
        /// 属性名在本着色器里不存在就跳过（照 Unity：设了没用到的属性不报错也不生效）。
        /// </summary>
        private void ApplyProperties(ShaderProperties source, SamplerState sampler)
        {
            IReadOnlyDictionary<string, ShaderProperty> properties = source.Properties;
            if (properties.Count == 0) return;

            foreach (KeyValuePair<string, ShaderProperty> pair in properties)
            {
                JSObject? location = PropertyLocation(pair.Key);
                if (location is null) continue;

                ShaderProperty property = pair.Value;
                switch (property.Type)
                {
                    case ShaderPropertyType.Int:
                        JSBind_WEBGL20.Uniform1i(location, property.Int);
                        break;
                    case ShaderPropertyType.Vector:
                    case ShaderPropertyType.Color:
                        JSBind_WEBGL20.Uniform4f(location, property.Vector.X, property.Vector.Y, property.Vector.Z, property.Vector.W);
                        break;
                    case ShaderPropertyType.Matrix:
                        WriteMatrix(property.Matrix, _matrixBuffer);
                        JSBind_WEBGL20.UniformMatrix4fv(location, 0, _matrixBuffer);
                        break;
                    case ShaderPropertyType.Texture:
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
        /// 把矩阵写成 16 个 float 的小端字节流交给 WebGL（列主序 + 转置抵消，见 <see cref="WebGL_ShaderProgram_2D_Default"/> 说明）。
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
