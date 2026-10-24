using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 着色器资产（照 Unity 的 <c>Shader</c>）：把"着色器名 + 顶点/片元源码"登记成可复用的资源，由
    /// <see cref="Find"/> 按名查找。它只负责<b>编译一次并持有共享的 GL/WGSL 程序</b>（构造时即用传入的
    /// <see cref="GraphicsDevice"/> 编译，非懒编译），本身不存任何 uniform 值——
    /// "每个材质不同参数"由 <see cref="ShaderEffect"/>（各自的 uniform 字典）或 <see cref="ShaderPropertyBlock"/>
    /// （这一次绘制的覆盖值）承载。
    /// <para>
    /// 对照 Unity：<c>Shader</c> 是资产（源码 + 名字），<c>Material</c> 是引用它的实例并带各自参数；
    /// 本引擎里 <see cref="ShaderEffect"/> 等价"程序 + 默认参数"。所以本类的
    /// <see cref="CreateEffectInstance"/> = Unity 里 <c>new Material(shader)</c>：同一 Shader、多个材质各有各的
    /// uniform 字典、共用同一份编译好的程序，互不干扰、也不重复编译（直接复用底层 <see cref="IShaderProgram"/>）。
    /// </para>
    /// <para>
    /// 这正是解决"同一 Shader 用在不同材质、参数却互相干扰"的法门：别让多个材质指向<b>同一个</b>
    /// <see cref="ShaderEffect"/>（那样共享的是同一本 uniform 字典，后写覆盖先写）；而是各自
    /// <see cref="CreateEffectInstance"/>，它们共享程序、各有各的字典。
    /// </para>
    /// </summary>
    public sealed class Shader
    {
        /// <summary>按名字登记的着色器表（<see cref="Find"/> 查的就是它）。同名后注册的覆盖先注册的。</summary>
        private static readonly Dictionary<string, Shader> Registry = new(StringComparer.Ordinal);

        /// <summary>着色器名 / 路径（照 Unity 的 "Custom/MyShader" 这类资源路径，仅作查找键与可读性）。</summary>
        public string Name { get; }

        /// <summary>顶点着色器源码（为 null 时回落到 <see cref="ShaderEffect.DefaultVertexSource"/> 标准精灵顶点）。</summary>
        public string VertexSource { get; }

        /// <summary>片元着色器源码（自定义后效的真正逻辑所在）。</summary>
        public string FragmentSource { get; }

        /// <summary>
        /// 编译好的共享着色器程序（<see cref="IShaderProgram"/>，WebGL = GLSL Program；WebGPU = 默认回落）。
        /// 构造时即用传入的 <see cref="GraphicsDevice"/> 编译一次（只读、非懒编译），之后所有实例共用这一份。
        /// </summary>
        private readonly IShaderProgram _program;

        /// <summary>默认 uniform 值（照 Unity 的 Shader 全局默认 / Material 默认值）：在 <see cref="CreateEffectInstance"/>
        /// 时会被<b>完整克隆</b>进每个新实例，使每个材质都从"全默认"起步——你只需覆盖不同的那几个，也不会出现
        /// "共享同一 Program 的多个材质互相串味"（因为默认值把 shader 用到的 uniform 全填上了）。应在创建实例前设置。</summary>
        private readonly ShaderPropertyBlock _defaults = new();
        public ShaderPropertyBlock Defaults => _defaults;

        /// <summary>
        /// 构造并登记一个着色器，并立即用 <paramref name="device"/> 把顶点/片元源码编译成共享程序（只读、非懒编译）。
        /// </summary>
        /// <param name="name">着色器名 / 路径（查找键）。</param>
        /// <param name="vertexSource">顶点着色器源码（null = 用标准精灵顶点 <see cref="ShaderEffect.DefaultVertexSource"/>）。</param>
        /// <param name="fragmentSource">片元着色器源码（必填，自定义后效逻辑）。</param>
        /// <param name="device">用于编译着色器的图形设备（必须已就绪）。</param>
        public Shader(string name, string? vertexSource, string fragmentSource, GraphicsDevice device)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            VertexSource = vertexSource ?? ShaderEffect.DefaultVertexSource;
            FragmentSource = fragmentSource ?? throw new ArgumentNullException(nameof(fragmentSource));
            Registry[name] = this;
            _program = device.CreateShaderEffect(FragmentSource, VertexSource).Program;
        }

        /// <summary>按名查找已登记的着色器（没找到返回 null，照 Unity 的 <c>Shader.Find</c>）。</summary>
        public static Shader? Find(string name)
            => name is null ? null : (Registry.TryGetValue(name, out Shader? s) ? s : null);

        /// <summary>
        /// 返回编译好的共享着色器程序（构造时即编译完毕，此处只是取出，不进行任何编译）。
        /// 同一 <see cref="Shader"/> 只编译一次；之后所有 <see cref="CreateEffectInstance"/> 都复用这一份。
        /// <para>当前仅 WebGL 后端真正编译自定义着色器；WebGPU 回落默认精灵着色器（效果不生效，页面照常）。</para>
        /// </summary>
        public IShaderProgram GetProgram() => _program;

        /// <summary>
        /// 产出一个"带自己 uniform 字典"的 <see cref="ShaderEffect"/>，但<b>共用</b> <see cref="GetProgram"/> 编译好的程序。
        /// 即 Unity 里 <c>new Material(shader)</c> 的等价物：同一 Shader、不同材质各有各的参数、互不干扰、也不重复编译。
        /// <para>
        /// 创建时会先把 <see cref="Defaults"/> 里的默认 uniform <b>完整克隆</b>进新实例（默认值先写，调用方随后 SetXxx 的
        /// 同名项再覆盖），因此每个实例都从"全默认"起步——你只需覆盖不同的那几个，且共享同一 Program 也不会串味。
        /// </para>
        /// <para>
        /// 注意：本方法返回的实例<b>不要</b>单独 <see cref="ShaderEffect.Dispose"/>——程序由本 <see cref="Shader"/> 持有，
        /// 误 Dispose 会删掉被所有实例共用的 GL/WGSL 程序。要释放请调用 <see cref="Dispose"/>。
        /// </para>
        /// </summary>
        public ShaderEffect CreateEffectInstance()
        {
            ShaderEffect effect = new ShaderEffect(_program, FragmentSource);
            CopyDefaults(effect);
            return effect;
        }

        /// <summary>把 <see cref="_defaults"/> 里的每一笔默认 uniform 完整拷进目标 <see cref="ShaderEffect"/>，
        /// 让目标从"全默认"起步（覆盖式：默认值先写，调用方随后 SetXxx 的同名项会再覆盖）。</summary>
        private void CopyDefaults(ShaderEffect target)
        {
            foreach (KeyValuePair<string, ShaderProperty> pair in _defaults.Properties)
            {
                string name = pair.Key;
                ShaderProperty p = pair.Value;
                switch (p.Type)
                {
                    case ShaderPropertyType.Int:
                        target.SetInt(name, p.Int);
                        break;
                    case ShaderPropertyType.Vector:
                    case ShaderPropertyType.Color:
                        target.SetVector(name, p.Vector);
                        break;
                    case ShaderPropertyType.Matrix:
                        target.SetMatrix(name, p.Matrix);
                        break;
                    case ShaderPropertyType.Texture:
                        target.SetTexture(name, p.Texture);
                        break;
                    default:
                        target.SetFloat(name, p.Float);
                        break;
                }
            }
        }

        /// <summary>释放本着色器持有的共享程序（会删掉被所有实例共用的 GL/WGSL 程序）。</summary>
        public void Dispose() => _program.Dispose();
    }
}
