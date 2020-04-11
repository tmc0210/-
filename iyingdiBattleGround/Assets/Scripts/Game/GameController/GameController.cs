﻿using BIF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController
{
#if UNITY_EDITOR
    readonly bool isTest = false;
#else
    readonly bool isTest = false;
#endif

    readonly static string[] herosUnlockChain = {
        "艾德温·范克里夫",
        "尤格萨隆",
        "火车王里诺艾",
        "贸易大王加里维克斯",
        "雷诺·杰克逊",
        "虚空之影瓦莉拉",
        "狗头人国王托瓦格尔",
        "砰砰博士" };

    #region public var

    public Player player;   //玩家,存储玩家控制的英雄
    public CardPile cardPile;   //玩家目前的总牌池,每局游戏进行复制
    public List<Card> defeatedEnemy = new List<Card>();
    
    public const int MaxLevel = 4;          // 最大关卡数
    
    public GameAnimationSetting gameAnimationSetting = null; // 动画控制器
    
    System.Random random = new System.Random();
    
    #endregion
    
    #region private var
    
    private bool isGotResponse = false;     // use for waiting response
    private int returnValue = 0;            // 存放协程的返回值
    //readonly static string startHeroName = "雷诺·杰克逊";


    #endregion

    #region init
    public GameController(GameAnimationSetting gameAnimationSetting)
    {
        //读取玩家解锁了的宝藏,英雄,卡牌等信息
        LoadLockCard();
        SetUpHeroUnlockThing();

        // 初始化
        this.gameAnimationSetting = gameAnimationSetting;
        cardPile = CardPile.GetEmptyCardPile();
        player = null;

    }

    private void SetUpHeroUnlockThing()
    {
        herosUnlockChain
            .Map(CardBuilder.SearchCardByName)
            .Where(card => card != null)
            .ToList()
            .DoubleZip()
            .Map(pair => {
                pair.Item1.unlockDescription = "使用" + pair.Item2.name + "进入挑战4/4时解锁";
            });
    }


    public IEnumerator EGameStart()
    {
        defeatedEnemy.Clear();
        yield return StartCoroutine(EStartSence());
        
        if (returnValue == 0) // 开始游戏
        {
    
        }
        else
        {
            yield return StartCoroutine(EShowCollection());
            yield return StartCoroutine(EGameStart());
        }


        yield return StartCoroutine(ESelectHero());
        if (returnValue == -1) // 返回
        {
            yield return StartCoroutine(EGameStart());
        }

        EnemyManager.CreateEnemy();
        cardPile = CardPile.GetEmptyCardPile();

        //FillCardPileWith1StarMinion(cardPile);

        //yield return StartCoroutine(ESelectTreasure());

        for (int i = 0; i < 14; i++)
        {
            Debug.Log("选择第" + (i+1) + "堆");
            yield return StartCoroutine(ESelectCardForCardPile(i));
        }
    
    	//int tmp = random.Next(4); //For Test
    
        for (int i = 0; i < MaxLevel; i++)
        {
            yield return StartCoroutine(EStartLevel(i));

            if (returnValue == 0) // win
            {
                if (i != 3)
                {
                    yield return StartCoroutine(ESelectTreasure());
                    for (int j = 14 + 2 * i; j < 16 + 2 * i; j++)
                    {
                        yield return StartCoroutine(ESelectCardForCardPile(j));
                    }
                }
            }
            else // lose
            {
                yield return StartCoroutine(ESummary());
            }
        }
        yield return StartCoroutine(EGameStart());
    }

    private IEnumerator ESummary()
    {
        gameAnimationSetting.selectPileAction = n => {
            isGotResponse = true;
        };
        gameAnimationSetting.ShowSummary(defeatedEnemy, player.treasures, cardPile);

        yield return StartCoroutine(WaitForGotResponse());

        yield return StartCoroutine(EGameStart());
    }


    #endregion

    #region gamelogic


    /// <summary>
    /// returnValue 0:开始游戏 1：收藏馆
    /// </summary>
    /// <returns></returns>
    private IEnumerator EStartSence()
    {
        gameAnimationSetting.StartSence();
        gameAnimationSetting.PlayAudioMain();
        gameAnimationSetting.startSenceAction = n =>
        {
            isGotResponse = true;
            returnValue = n;
        };
        yield return StartCoroutine(WaitForGotResponse());
    }
    
    /// <summary>
    /// 进行一次选牌
    /// </summary>
    private IEnumerator ESelectCardForCardPile(int i)
    {
        var tuple = SelectCard.GetCards(i); 
        List<Card> firstPile = tuple.Item1.Shuffle().Take(3).ToList();
        List<Card> secondPile = tuple.Item2.Shuffle().Take(3).ToList();
        List<Card>[] piles = new List<Card>[2] { firstPile , secondPile};


        gameAnimationSetting.SelectCardForCardPile(firstPile, secondPile, cardPile, tuple.Item3, tuple.Item4);
        gameAnimationSetting.selectPileAction = n =>
        {
            Debug.Log("选择了 " + piles[n].Select(card=>card.name).StringJoin(","));
            isGotResponse = true;
            piles[n].ForEach(card =>
            {
                cardPile.AddCard(card, Const.numOfMinionsInCardPile[card.star - 1]);
                SelectCard.Remove(card);
            });
        };

        if (isTest)
        {
            gameAnimationSetting.selectPileAction.Invoke(0);
            yield break;
        }

        yield return StartCoroutine(WaitForGotResponse());
    
        //Debug.Log("选择完毕");
    }


    public IEnumerator EStartLevel(int n)
    {
        int level = n + 1;
        Debug.Log("进入第" + level + "关");

        if (level == 4)
        {
            UnlockHeroByHero(player.hero);
        }

        BoardInitArgs boardInitArgs = GetFilledBoardInitArgs();
        boardInitArgs.level = level;
        boardInitArgs.enemy = EnemyManager.GetEnemyByLevel(level);
        CardPile cardPile = new CardPile();
        cardPile.InitCardPileFully();
        boardInitArgs.enemy.Init(cardPile);
        //boardInitArgs.enemy.Init(cardPile);
        boardInitArgs.enemy.SetLevel(boardInitArgs.level);

        yield return StartCoroutine(EIntroEnmey(boardInitArgs.enemy.player, level));

        gameAnimationSetting.selectPileAction = i => {
            returnValue = i;
            isGotResponse = true;
        };
        gameAnimationSetting.StartNewGame(boardInitArgs);
        gameAnimationSetting.PlayAudioBattle();

        yield return StartCoroutine(WaitForGotResponse());

        if (returnValue == 0)
        {
            Card hero = boardInitArgs.enemy.player.hero;
            defeatedEnemy.Add(CardBuilder.GetCard(hero.id).NewCard());
        }
    }

    private IEnumerator EIntroEnmey(Player player, int level)
    {
        gameAnimationSetting.IntroEnemy(player, level);
        gameAnimationSetting.selectCardAction = card => {
            isGotResponse = true;
        };
        yield return StartCoroutine(WaitForGotResponse());
    }

    private IEnumerator ESelectHero()
    {
        List<Card> heros = CardBuilder.AllCards.FilterValue(card => card.cardType == CardType.Hero && !card.isToken);


        // 发送消息
        gameAnimationSetting.SelectHero(GetUnlockedHero(), GetLockedHero());
    
        // 设置回调
        gameAnimationSetting.selectCardAction = hero => {
            if (hero == null)
            {
                returnValue = -1;
            }
            else
            {
                returnValue = 0;
                player = new Player(hero.NewCard());
                DataCollection.SelectHero(hero);
            }
            gameAnimationSetting.selectCardAction = null;
            isGotResponse = true;
        };
    
        yield return StartCoroutine(WaitForGotResponse());
    }
    private IEnumerator ESelectTreasure()
    {
        List<Card> treasures = new List<Card>();
        foreach (Card card in CardBuilder.AllCards.FilterValue(card => card.tag.Contains("宝藏")))
        {
            if (!player.treasures.Contains(card) || !card.tag.Contains("唯一"))
            {
                treasures.Add(card);
            }
        }
        
        // 发送消息
        gameAnimationSetting.SelectTreasure(treasures.Shuffle().Take(3).ToList());

        // 设置回调
        gameAnimationSetting.PlayAudioMain();
        gameAnimationSetting.selectCardAction = card => {
            isGotResponse = true;
            gameAnimationSetting.selectCardAction = null;
            player.treasures.Add(card.NewCard());
            UnlockCard(card.id);


            Debug.Log("treasure select:" + card.name);
        };
    
        yield return StartCoroutine(WaitForGotResponse());
    }
    
    private IEnumerator EShowCollection()
    {
        gameAnimationSetting.ShowCollection();
        gameAnimationSetting.selectCardAction = card =>
        {
            isGotResponse = true;
        };
        gameAnimationSetting.PlayAudioCollection();
        yield return StartCoroutine(WaitForGotResponse());
    }

    #endregion

    #region tool function for card lock
    public static void UnlockHeroByHero(Card hero)
    {
        int index = herosUnlockChain.ToList().IndexOf(hero.name);
        if (index >= 0 && index + 1 < herosUnlockChain.Length)
        {
            Card newHero = CardBuilder.SearchCardByName(herosUnlockChain[index + 1]);
            if (newHero != null)
            {
                UnlockCard(newHero.id);
            }
        }
    }
    public static void UnlockCard(int card)
    {
        if (CardBuilder.GetCard(card).Lock)
        {
            CardBuilder.GetCard(card).Lock = false;
            SaveLockCard();
        }
    }
    public static void SaveLockCard()
    {
        Debug.Log("save");
        string unlockCards = CardBuilder.AllCards
            .FilterValue(card => !card.Lock)
            .Select(card => card.id)
            .ToList()
            .Serialize();
        PlayerPrefs.SetString("unlockCards", unlockCards);

    }
    public static void LoadLockCard()
    {
        string unlockCards = PlayerPrefs.GetString("unlockCards", "");
    
        List<int> ids = unlockCards.ParseListInt();
        ids.Select(id => CardBuilder.GetCard(id))
            .Map(card => {
                card.Lock = false;
            });
        Debug.Log("load card " + ids.Count);
        Card defaultHero = CardBuilder.SearchCardByName(herosUnlockChain[0]);
        if (defaultHero != null)
        {
            defaultHero.Lock = false;
        }
    }
    
    public List<Card> GetLockedHero()
    {
        //return CardBuilder.AllCards
        //    .FilterValue(card => !card.isToken && card.Lock && card.cardType == CardType.Hero && !card.tag.Contains("Boss"));

        return herosUnlockChain
            .Map(CardBuilder.SearchCardByName)
            .Where(card=>card!=null)
            .Where(card=>card.Lock)
            .ToList();

    }
    public List<Card> GetUnlockedHero()
    {
        //return CardBuilder.AllCards
        //    .FilterValue(card => !card.isToken && !card.Lock && card.cardType == CardType.Hero && !card.tag.Contains("Boss"));


        return herosUnlockChain
            .Map(CardBuilder.SearchCardByName)
            .Where(card => card != null)
            .Where(card => !card.Lock)
            .ToList();
    }

    public static List<Card> GetAllUnlockedCard()
    {
        return CardBuilder.AllCards.FilterValue(card => !card.isToken && card.cardType == CardType.Minion && !card.Lock)
            .OrderByDescending(card => card.star)
            .ToList();
    }
    public static List<Card> GetAllLockedCard()
    {
        return CardBuilder.AllCards.FilterValue(card => !card.isToken && card.cardType == CardType.Minion && card.Lock)
            .OrderByDescending(card => card.star)
            .ToList();
    }
    public static List<Card> GetAllCard()
    {
        return CardBuilder.AllCards.FilterValue(card => !card.isToken && card.cardType == CardType.Minion)
            .OrderBy(card => card.star)
            .ToList();
    }

    public static List<Card> GetAllTreasureCard()
    {
        return CardBuilder.AllCards.FilterValue(card => !card.isToken && card.tag.Contains("宝藏"));
    }

    #endregion

    #region tool function

    public BoardInitArgs GetTestBoardInitArgs()
    {
        EnemyManager.CreateEnemy();
        CardPile cardPile = new CardPile();
        cardPile.InitCardPileFully();
    
        int tmp = random.Next(4) + 1;
    
        Enemy enemy = EnemyManager.enemies[tmp - 1];
        enemy.Init(cardPile);
    
        Player player = this.player;
        if (player == null)
        {
            player = new Player(CardBuilder.SearchCardByName("雷诺·杰克逊").NewCard());
        }
    
        BoardInitArgs boardInitArgs = new BoardInitArgs
        {
            player = player,
            enemy = enemy,
            cardPile = cardPile,
            level = tmp
        };
        //Debug.Log(boardInitArgs.player + "this");
        return boardInitArgs;
    }
    public BoardInitArgs GetFilledBoardInitArgs()
    {
        CardPile cardPile = new CardPile();
        cardPile.AddMap(this.cardPile.cardPile);
        BoardInitArgs boardInitArgs = new BoardInitArgs
        {
            player = player.Copy(),
            cardPile = cardPile,
            enemy = null,
            level = 1
        };
        return boardInitArgs;
    }
    
    public void FillCardPileWith1StarMinion(CardPile cardPile)
    {
        CardBuilder.AllCards
            .FilterValue(card => card.cardType == CardType.Minion && !card.isToken && card.star == 1)
            .Shuffle()
            .Take(5)
            .Map(card => {
                cardPile.AddCard(card, 18);
            });
    }
    
    #endregion
    
    #region tool function for Coroutine
    
    private Coroutine StartCoroutine(IEnumerator enumerator)
    {
        return gameAnimationSetting.StartCoroutine(enumerator);
    }


    /// <summary>
    /// wait until isGotResponse==true
    /// </summary>
    /// <returns></returns>
    private IEnumerator WaitForGotResponse()
    {
        isGotResponse = false;
        while (!isGotResponse)
        {
            yield return null;
        }
    }
    
    #endregion
}



public struct BoardInitArgs
{
    public Player player;
    public Enemy enemy;
    public CardPile cardPile;
    public int level;
}