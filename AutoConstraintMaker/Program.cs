﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        static Dictionary<string, string> mmdDivaBon = new Dictionary<string, string>()
        {
            {"全ての親2", "gblctr"},
            {"全ての親", "kg_ya_ex"},
            {"センター", "n_hara_cp"},
            {"グルーブ", "kg_hara_y"},
            {"腰", "n_hara_b_wj_ex"},
            {"下半身", "kl_kosi_etc_wj"},
            {"左目", "n_eye_l_wj_ex"},
            {"右目", "n_eye_r_wj_ex"},
            {"上半身", "kl_kosi_etc_wj"},
            {"上半身2", "kl_mune_b_wj"},
            {"上半身1", "n_hara_c_wj_ex"},
            {"首", "n_kubi_wj_ex"},
            {"頭", "j_kao_wj"},
            {"左肩", "kl_waki_l_wj"},
            {"左腕", "n_skata_l_wj_cd_ex"},
            {"左手", "n_hiji_l_wj_ex"},
            {"左手首", "n_ste_l_wj_ex"},
            {"右肩", "kl_waki_r_wj"},
            {"右腕", "n_skata_r_wj_cd_ex"},
            {"右手", "n_hiji_r_wj_ex"},
            {"右手首", "n_ste_r_wj_ex"},
            {"左親指０", "nl_oya_l_wj"},
            {"左親指１", "nl_oya_b_l_wj"},
            {"左親指２", "nl_oya_c_l_wj"},
            {"左人指１", "nl_hito_l_wj"},
            {"左人指２", "nl_hito_b_l_wj"},
            {"左人指３", "nl_hito_c_l_wj"},
            {"左中指１", "nl_naka_l_wj"},
            {"左中指２", "nl_naka_b_l_wj"},
            {"左中指３", "nl_naka_c_l_wj"},
            {"左薬指１", "nl_kusu_l_wj"},
            {"左薬指２", "nl_kusu_b_l_wj"},
            {"左薬指３", "nl_kusu_c_l_wj"},
            {"左小指１", "nl_ko_l_wj"},
            {"左小指２", "nl_ko_b_l_wj"},
            {"左小指３", "nl_ko_c_l_wj"},
            {"右親指０", "nl_oya_r_wj"},
            {"右親指１", "nl_oya_b_r_wj"},
            {"右親指２", "nl_oya_c_r_wj"},
            {"右人指１", "nl_hito_r_wj"},
            {"右人指２", "nl_hito_b_r_wj"},
            {"右人指３", "nl_hito_c_r_wj"},
            {"右中指１", "nl_naka_r_wj"},
            {"右中指２", "nl_naka_b_r_wj"},
            {"右中指３", "nl_naka_c_r_wj"},
            {"右薬指１", "nl_kusu_r_wj"},
            {"右薬指２", "nl_kusu_b_r_wj"},
            {"右薬指３", "nl_kusu_c_r_wj"},
            {"右小指１", "nl_ko_r_wj"},
            {"右小指２", "nl_ko_b_r_wj"},
            {"右小指３", "nl_ko_c_r_wj"},
            {"左足", "j_momo_l_wj"},
            {"左ひざ", "j_sune_l_wj"},
            {"左足首", "kl_asi_l_wj_co"},
            {"左足先", "kl_toe_l_wj"},
            {"右足", "j_momo_r_wj"},
            {"右ひざ", "j_sune_r_wj"},
            {"右足首", "kl_asi_r_wj_co"},
            {"右足先", "kl_toe_r_wj"},
            {"左足ＩＫ", "e_sune_l_cp"},
            {"右足ＩＫ", "e_sune_r_cp"},
        };


        // arg 0: target obj farc
        // arg 1: base obj farc for bone orientations
        static void Main(string[] args)
        {
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
