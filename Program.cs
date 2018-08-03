using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class Player
{
    private string description;
    /// <summary>
    /// Player health
    /// </summary>
    int health;
    /// <summary>
    /// Player max mana
    /// </summary>
    public int mana;
    /// <summary>
    /// Card count in the deck
    /// </summary>
    int deck;
    int rune;
    public Player(string description)
    {
        Game.Debug(description);

        this.description = description;
        
        var tokens = description.Split(' ');
        this.health = int.Parse(tokens[0]);
        this.mana = int.Parse(tokens[1]);
        this.deck = int.Parse(tokens[2]);
        this.rune = int.Parse(tokens[3]);           
    }
    public Player Clone()
    {
        return new Player(this.description);
    }
    public void Summon(Card card)
    {
        this.mana = this.mana - card.cost;
    }
    public bool CanUseCard(Card card)
    {
        Game.Debug($"card cost {card.cost} / my mana {this.mana}");

        return card.cost <= this.mana; 
    }
    
}
#region Commands
public abstract class Command
{
}
public class UseItemOnCreatureCommand : Command
{
    private readonly int itemId;
    private readonly int targetCreatureId;

    public UseItemOnCreatureCommand(Card item, Card targetCreature)
    {
        itemId = item.instanceId;
        targetCreatureId = targetCreature.instanceId;
    }
    
    public override string ToString()
    {
        return $"USE {itemId.ToString()} {targetCreatureId.ToString()}";
    }
}

public class UseItemCommand : Command
{
    private readonly int itemId;

    public UseItemCommand(Card item)
    {
        itemId = item.instanceId;
    }
    
    public override string ToString()
    {
        return $"USE {itemId.ToString()} -1";
    }
}

public class PassCommand : Command
{
    public override string ToString()
    {
        return "PASS";
    }
}
public class PickCommand : Command
{
    private readonly int nb;
    public PickCommand(int nb)
    {
        this.nb = nb;
    }
    public override string ToString()
    {
        return $"PICK {nb.ToString()}";
    }
}

public class SummonCommand : Command
{
    private readonly int creatureId;
    public SummonCommand(Card card)
    {
        this.creatureId = card.instanceId;
    }
    public override string ToString()
    {
        return $"SUMMON {creatureId.ToString()}";
    }
}
public class AttackCreatureCommand : Command
{
    private readonly int fromCreatureId;
    private readonly int targetCreatureId;
    public AttackCreatureCommand(Card from, Card target)
    {
        this.fromCreatureId = from.instanceId;
        this.targetCreatureId = target.instanceId;
    }
    public override string ToString()
    {
        return $"ATTACK {fromCreatureId.ToString()} {targetCreatureId.ToString()}";
    }
}

public class AttackOpponentCommand : Command
{
    private readonly int fromCreatureId;
    public AttackOpponentCommand(Card fromCard)
    {
        this.fromCreatureId = fromCard.instanceId;
    }
    public override string ToString()
    {
        return $"ATTACK {fromCreatureId.ToString()} -1";
    }
}
#endregion
public enum Location
{
    MyHand = 0,
    PlayerSide = 1,
    OpponentSide = -1
}
public enum CardType
{
    Creature = 0,
    GreenItem = 1,
    RedItem = 2,
    BlueItem = 3
}
public class Card
{
    public readonly Location location;
    public readonly int instanceId;
    public readonly int attack;
    public readonly int defense;
    public readonly int cost;
    public readonly int myHealthChange;
    public readonly int opponentHealthChange;
    private readonly  string abilities;
    public readonly CardType CardType;
    public Card(string description)
    {
        var inputs = description.Split(' ');
        int cardNumber = int.Parse(inputs[0]);
        this.instanceId = int.Parse(inputs[1]);
        this.location = (Location)int.Parse(inputs[2]);
        this.CardType = (CardType)int.Parse(inputs[3]);
        this.cost = int.Parse(inputs[4]);
        this.attack = int.Parse(inputs[5]);
        this.defense = int.Parse(inputs[6]);
        this.abilities = inputs[7];
        this.myHealthChange = int.Parse(inputs[8]);
        this.opponentHealthChange = int.Parse(inputs[9]);
        int cardDraw = int.Parse(inputs[10]);
    }

    public bool HasBreakthrough => abilities[0] == 'B';
    public bool HasCharge => abilities[1] == 'C';
    public bool HasDrain => abilities[2] == 'D';
    public bool HasGuard => abilities[3] == 'G';
    public bool HasLethal => abilities[4] == 'L';
    public bool HasWard => abilities[5] == 'W';
    
    public int ComputeScoreForDraft()
    {
        return this.attack + this.myHealthChange - this.opponentHealthChange - this.cost;
    }
   
}

public interface IDraftAI
{
    Command GetCommand();
}
public class DraftAI : IDraftAI
{
    private readonly Card[] cards;
    public DraftAI(Card[] cards)
    {
        this.cards = cards;
    }
    public Command GetCommand()
    {
        var bestCardScore = int.MinValue;
        var bestCardIndex = 0;

        for(var i = 0; i < cards.Length; i++)
        {
            var currentCard = cards[i];
            var currentScore = currentCard.ComputeScoreForDraft();
            if(currentScore > bestCardScore)
            {
                bestCardIndex = i;
                bestCardScore = currentScore;
            }
        }

        return new PickCommand(bestCardIndex);
    }
}
public interface AbstractBattleAI
{
    Command[] GetCommands();
}

public class BattleAI : AbstractBattleAI
{
    private readonly Player me;
    private readonly Player opponent;
    private readonly Card[] cards;
    
    public BattleAI(Player me, Player opponent, Card[] cards)
    {
        this.me = me;
        this.opponent = opponent;
        this.cards = cards;
    }
    public Command[] GetCommands()
    {
        var commands = new List<Command>();

        commands.AddRange(SummonCreatures(out List<Card> cardsWithCharge));

        var myCardsInDashboard = this.cards
                .Where(c => c.location == Location.PlayerSide)
                .Union(cardsWithCharge)
                .ToList();

        commands.AddRange(AttackOpponentGuards(myCardsInDashboard));
        commands.AddRange(AttackOpponent(myCardsInDashboard));
        commands.AddRange(UseBlueItems(myCardsInDashboard));

        return commands.ToArray();
    }

    private List<UseItemCommand> UseBlueItems(List<Card> remainingCardsInDashboard)
    {
        var commands = new List<UseItemCommand>();

        var blueItems = this.cards
            .Where(c => c.location == Location.MyHand && c.CardType == CardType.BlueItem)
            .ToArray();

        foreach(var blueItem in blueItems)
        {
            if(me.CanUseCard(blueItem))
            {
                commands.Add(new UseItemCommand(blueItem));
                remainingCardsInDashboard.Remove(blueItem);
            }
        } 
        return commands;
    }

    private List<AttackCreatureCommand> AttackOpponentGuards(List<Card> remainingCardsInDashboard)
    {
        var commands = new List<AttackCreatureCommand>();
        var enemyGuards = this.cards
            .Where(c => c.location == Location.OpponentSide && c.HasGuard)
            .OrderByDescending( c => c.defense)
            .ToArray();

        for(int i = 0; i < enemyGuards.Length; i++)
        {
            commands.AddRange( AttackEnemyCreature(enemyGuards[i], remainingCardsInDashboard));
        }

        return commands;
    }

    private List<AttackCreatureCommand> AttackEnemyCreature(Card enemyCard, List<Card> remainingCardsInDashboard)
    {
        var myOrderedcards = remainingCardsInDashboard
            .OrderByDescending(c => c.attack)
            .ToArray();
        
        var commands = new List<AttackCreatureCommand>();
        var index = 0;
        var remainingDefense = enemyCard.defense;

        while(remainingDefense > 0 && index < myOrderedcards.Length)
        {
            commands.Add(new AttackCreatureCommand(myOrderedcards[index], enemyCard));
            remainingDefense = remainingDefense - myOrderedcards[index].attack;
            remainingCardsInDashboard.Remove(myOrderedcards[index]);
            index++;
        }

        return commands;
    }

    private List<AttackOpponentCommand> AttackOpponent(List<Card> remainingCardsInDashboard)
    {
        return remainingCardsInDashboard
                .Select( c => new AttackOpponentCommand(c))
                .ToList();              
    }

    private List<Command> SummonCreatures(out List<Card> cardsWithCharge)
    {
        cardsWithCharge = new List<Card>();

        var commands = new List<Command>();
        var me = this.me.Clone();
        var creaturesInMyHand = this.cards
                        .Where(c => c.location == Location.MyHand && c.CardType == CardType.Creature)
                        .OrderByDescending(c => c.attack)
                        .ToArray();

        foreach(var creatureInMyHand in creaturesInMyHand)
        {
            if(me.CanUseCard(creatureInMyHand) && MySummonedCreaturesDoesNotExceed(Game.MaxCreatures))
            {
                me.Summon(creatureInMyHand);
                commands.Add(new SummonCommand(creatureInMyHand));

                if(creatureInMyHand.HasCharge)
                {
                    cardsWithCharge.Add(creatureInMyHand);
                }
            }
        }
        return commands;
    }

    private bool MySummonedCreaturesDoesNotExceed(int max)
    {
        int summonedCreatures = this.cards.Count(c => c.location == Location.PlayerSide);
        
        return summonedCreatures <= max;
    }
}

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
public class Game
{
    public const int MaxCreatures = 6;
    public static void Debug(string message)
    {
        Console.Error.WriteLine(message);
    }
    static void Main(string[] args)
    {
        // game loop
        while (true)
        {
            var me = new Player(Console.ReadLine());
            var opponent  = new Player(Console.ReadLine());

            var isDraftPhase = me.mana == 0;

            int opponentHand = int.Parse(Console.ReadLine());
            int cardCount = int.Parse(Console.ReadLine());

            Card[] cards = new Card[cardCount];

            for (int i = 0; i < cardCount; i++)
            {
                var description = Console.ReadLine();
                Game.Debug(description);
                cards[i] = new Card(description);
            }

            if(isDraftPhase)
            {
                var draftAI = new DraftAI(cards);
                var command = draftAI.GetCommand();
                Console.WriteLine(command.ToString());
            }
            else
            {
                var battleAI = new BattleAI(me, opponent, cards);
                var commands = battleAI.GetCommands();

                Console.WriteLine(string.Join(";", commands.Select(c => c.ToString()).ToArray()));
            }
        }
    }
}