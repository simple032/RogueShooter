using System;
using System.Collections.Generic;
using UnityEngine;
using RogueShooter.Maze;
using RogueShooter.Player;
using RogueShooter.Spawning;
using RogueShooter.Build;
namespace RogueShooter.Demo
{
    /// <summary>One seeded inventory for the final safe room; transactions keep the screen open.</summary>
    public sealed class FinalPrepShop : MonoBehaviour
    {
        Stage1MazeDemo _demo; MazeNode _room; int _seed;
        ShopShelf[] _shelves;
        public ShopShelf[] Shelves => _shelves == null ? Array.Empty<ShopShelf>() : (ShopShelf[])_shelves.Clone();
        public bool Near => _demo != null && _demo.PlayerBody != null && Vector2.Distance(_demo.PlayerBody.position,transform.position)<=2.4f;
        public bool CanOpen => _demo != null && _demo.ReadyForFinalShop && Near;
        public void Bind(Stage1MazeDemo demo,MazeNode room,int seed)
        {
            _demo=demo;_room=room;_seed=seed;transform.position=new Vector3(room.Center.X,room.Center.Y,0);
            var supply=Resources.Load<Sprite>("JianHaiReward/jh_iso_prop_supply_00");
            if(supply==null)supply=Stage1IsoArt.LoadSprite("Assets/Resources/JianHaiReward/jh_iso_prop_supply_00.png");
            if(supply!=null)
            {
                var counter=new GameObject("Supply counter");
                counter.transform.SetParent(transform,false);
                var renderer=counter.AddComponent<SpriteRenderer>();
                renderer.sprite=supply;
                renderer.sortingOrder=2;
            }
            else
            {
                var pieces=new List<GameObject>();
                Stage1IsoArt.Band("Supply dais",transform.position+Vector3.down*.35f,new Vector2(3.4f,.7f),new Color(.28f,.26f,.20f),-1,transform,pieces);
                Stage1IsoArt.Band("Supply table",transform.position+Vector3.up*.15f,new Vector2(2.8f,.8f),new Color(.34f,.27f,.16f),1,transform,pieces);
                for(int i=0;i<3;i++)Stage1IsoArt.Band("Supply vessel "+i,transform.position+new Vector3((i-1)*.85f,.6f),new Vector2(.42f,.55f),i==1?new Color(.48f,.58f,.64f):new Color(.67f,.52f,.27f),2,transform,pieces);
            }
        }
        public void EnsureStock()
        {
            if(_shelves!=null)return;
            RewardCatalog.BindPoolContext(StageId.S3,_demo.RunBuild.OwnedRewardIds);
            _shelves=ShopStock.RollShelves(new System.Random(unchecked(_seed^0x510F13)),_demo.RunBuild.Regression);
        }
        public void Open()
        {
            if(!CanOpen)return;
            EnsureStock();_demo.ShowFinalShop(Cards(),Caption());
        }
        string Caption()=>"遗库补给 · "+_demo.RunBuild.Gold+" 金 · 1–6 购买 · E / Esc 关闭";
        RewardCardData[] Cards()
        {
            var cards=new RewardCardData[_shelves.Length];var vitals=_demo.PlayerBody.GetComponent<PlayerVitals>();
            for(int i=0;i<cards.Length;i++)
            {
                var shelf=_shelves[i];string mark=(i+1)+" · "+(shelf.Sold?"已售":shelf.IsHeal&&vitals.Hp>=vitals.MaxHp?"生命已满":_demo.RunBuild.Gold<shelf.Price?"金币不足":ShopStock.RoleLabel(shelf.ContentRole));
                cards[i]=RewardPresent.ToCard(shelf.Id,shelf.Tier,shelf.Price+"金",mark,i,shelf.Sold);
                cards[i].Disabled=shelf.Sold||_demo.RunBuild.Gold<shelf.Price||shelf.IsHeal&&vitals.Hp>=vitals.MaxHp||!_demo.RunBuild.CanReceive(shelf.Id);
            }
            return cards;
        }
        public bool TryBuy(int index)
        {
            if(!CanOpen||!_demo.FinalShopOpen||_shelves==null||index<0||index>=_shelves.Length)return false;
            var shelf=_shelves[index];var vitals=_demo.PlayerBody.GetComponent<PlayerVitals>();
            if(shelf.Sold||shelf.IsHeal&&vitals.Hp>=vitals.MaxHp||!RewardCatalog.TryGet(shelf.Id,out var row)){RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_reject",this);return false;}
            if(!_demo.RunBuild.TryShopBuy(shelf.Price,1,shelf.Id)){RogueShooter.Audio.JianHaiAudio.Emit("jh_ui_reject",this);return false;}
            RogueShooter.Audio.JianHaiAudio.Emit("jh_shop_purchase",this);
            shelf.Sold=true;_shelves[index]=shelf;_demo.ApplyShopReward(row);
            _demo.ShowFinalShop(Cards(),Caption());
            Debug.Log("[FinalShop] bought "+shelf.Id+" gold="+_demo.RunBuild.Gold+" build="+_demo.RunBuild.BuildCount);
            return true;
        }
    }
}
