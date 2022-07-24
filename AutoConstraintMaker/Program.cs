using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MikuMikuLibrary.IO;
using MikuMikuLibrary.Archives;
using MikuMikuLibrary.Objects;
using MikuMikuLibrary.Textures;
using MikuMikuLibrary.Skeletons;
using MikuMikuLibrary.Databases;
using MikuMikuLibrary.Objects.Extra.Blocks;
using MikuMikuLibrary.Extensions;


namespace AutoConstraintMaker
{
    internal class Program
    {

        // arg 0: target obj farc
        // arg 1: base obj farc for bone orientations
        static void Main(string[] args)
        {
            string boneMap = File.ReadAllText("map.json");
            Dictionary<string, string> mmdDivaBon = JsonConvert.DeserializeObject<Dictionary<string, string>>(boneMap);
            // it assumes that the object farc is in an objset folder, with the bone_data just outside it
            // fuck that im embedding the bone data. fuck it im crazy! kash doll reaction video
            // https://stackoverflow.com/questions/3314140/how-to-read-embedded-resource-text-file
            //var assembly = Assembly.GetExecutingAssembly();
            //var resourceName = "AutoConstraintMaker.Properties.Resources.bone_data.bin";
            byte[] boneDataArray = AutoConstraintMaker.Properties.Resources.bone_data_bin;
            Stream boneDataStream = new MemoryStream(boneDataArray);
            var embeddedBoneDb = boneDataStream;
            var boneDb = BinaryFile.Load<BoneDatabase>(embeddedBoneDb);
            var objFarc = BinaryFile.Load<FarcArchive>(args[0]);
            var objBinSrc = objFarc.Open(objFarc.First(x => x.EndsWith("_obj.bin")), EntryStreamMode.MemoryStream);
            var objSet = BinaryFile.Load<ObjectSet>(objBinSrc);

            var baseObjFarc = BinaryFile.Load<FarcArchive>(args[1]);
            var baseObjBinSrc = baseObjFarc.Open(baseObjFarc.First(x => x.EndsWith("_obj.bin")), EntryStreamMode.MemoryStream);
            var baseObjSet = BinaryFile.Load<ObjectSet>(baseObjBinSrc);

            var blocks = new List<MikuMikuLibrary.Objects.Extra.IBlock>();
            var memStream = new MemoryStream();

            foreach (var obj in objSet.Objects)
            {

                // fixing bone orientations using bones from the base obj farc (arg 1)
                // to the target obj farc (arg 0)
                foreach (var bone in obj.Skin.Bones)
                {
                    mmdDivaBon.TryGetValue(bone.Name, out var srcbonename);
                    var srcbone = baseObjSet.Objects[0].Skin.Bones.FirstOrDefault(x => x.Name == srcbonename);

                    if (srcbone == null)
                        continue;

                    Matrix4x4.Invert(bone.InverseBindPoseMatrix, out var bibpm);
                    Matrix4x4.Decompose(bibpm, out var boneScale, out var boneRot, out var boneTrans);
                    Matrix4x4.Invert(srcbone.InverseBindPoseMatrix, out var sbibpm);
                    Matrix4x4.Decompose(sbibpm, out var srcboneScale, out var srcboneRot, out var srcboneTrans);

                    var newbon = Matrix4x4.CreateTranslation(boneTrans);
                    var transed = newbon * sbibpm;
                    transed.Translation = boneTrans;

                    Matrix4x4.Invert(transed, out var outbon);
                    bone.InverseBindPoseMatrix = outbon;

                }

                obj.Skin.Blocks.Clear();

                // root bone expression block
                var exp_Root = new ExpressionBlock()
                {
                    Name = "RootBone",
                    ParentName = "n_hara_cp",
                    Position = new Vector3(0, (float)-1.2, 0),
                    Rotation = new Vector3(0, 0, 0),
                    Scale = Vector3.One,
                };
                exp_Root.Expressions.Add("= 0 v 0.RootBone");
                exp_Root.Expressions.Add("= 1 v 1.RootBone");
                exp_Root.Expressions.Add("= 2 v 2.RootBone");
                exp_Root.Expressions.Add("= 3 v 3.RootBone");
                exp_Root.Expressions.Add("= 4 v 4.RootBone");
                exp_Root.Expressions.Add("= 5 v 5.RootBone");
                exp_Root.Expressions.Add("= 6 v 6.RootBone");
                exp_Root.Expressions.Add("= 7 v 7.RootBone");
                exp_Root.Expressions.Add("= 8 v 8.RootBone");
                blocks.Add(exp_Root);

                // main constraint making
                foreach (var bone in obj.Skin.Bones)
                {
                    if (mmdDivaBon.TryGetValue(bone.Name, out string sourceNodeName))
                    {
                        if (sourceNodeName.StartsWith("e_"))
                            continue;

                        Matrix4x4.Invert(bone.InverseBindPoseMatrix, out var bindPoseMatrix);
                        var matrix = Matrix4x4.Multiply(bindPoseMatrix,
                            bone.Parent?.InverseBindPoseMatrix ?? Matrix4x4.Identity);

                        Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation);
                        rotation = Quaternion.Normalize(rotation);

                        if (bone.Parent == null)
                            bone.Parent = new BoneInfo()
                            {
                                Name = "RootBone"
                            };

                        var oriConstraintBlock = new ConstraintBlock
                        {
                            Name = bone.Name,
                            Data = new OrientationConstraintData(),
                            Coupling = Coupling.Rigid,
                            ParentName = bone.Parent.Name,
                            Position = translation,
                            Rotation = rotation.ToEulerAngles(),
                            Scale = scale,
                            SourceNodeName = sourceNodeName
                        };
                        blocks.Add(oriConstraintBlock);


                    }
                }

                obj.Skin.Blocks.AddRange(blocks);
            }

            objSet.Save(memStream, true);
            objFarc.Add(objFarc.First(x => x.EndsWith("_obj.bin")), memStream, true, ConflictPolicy.Replace);
            objFarc.Save(args[0]);
        }
    }
}
