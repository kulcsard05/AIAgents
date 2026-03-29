using Azure;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Net;

namespace AiAgent
{

    internal class Program
    {

        static async Task Main(string[] args)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
            modelId: "llama3.1",
            apiKey: "ignore", // Local Ollama doesn't require an API key
            httpClient: new HttpClient { BaseAddress = new Uri("http://localhost:11434/v1") }

);

            // Meglévő pluginek regisztrálása...
            builder.Plugins.AddFromType<IpCheckerPlugin>();

            // ÚJ: A hálózati plugin hozzáadása az Agent eszköztárához
            builder.Plugins.AddFromType<NetworkPlugin>();

            Kernel kernel = builder.Build();

            //Viszgáló agent
            //-------------------------------------------------------------------------------
            // 1. Elkérjük a Chat szolgáltatást a felépített Kerneltől
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // 2. Létrehozzuk az Agentet! A ChatHistory első eleme a "System Prompt", ami definiálja a viselkedését.
            // Frissített, szigorúbb System Prompt a "Tool Call Leak" elkerülésére
            ChatHistory agentChat = new ChatHistory(
                "Te egy intelligens Csalásfelderítő Agent vagy. " +
                "Két feladatod van:\n" +
                "1. Használd a toolokat az IP címek és adatok ellenőrzésére!\n" +
                "2. A végső elemzésedet KÖTELEZŐEN az alábbi két szó valamelyikével kezdened: " +
                "Ha gyanús a dolog és eltérést találsz, írd a szöveg elejére: [CSALÁS]. " +
                "Ha minden rendben van, írd a szöveg elejére: [RENDBEN].\n" +
                "Ezután írd le röviden a technikai elemzésedet."
            );

            // 3. Adunk neki egy konkrét feladatot / kérdést
            agentChat.AddUserMessage("Valaki 5000 dollárért vett bitcoint a fiókomban a 193.45.12.8-as IP címről. Nem tudom, hogy én voltam-e véletlenül, vagy feltörtek. Kérlek nyomozd ki!");


            // 4. Beállítjuk a szigorú paramétereket (Temperature = 0.1 a megbízhatóságért)
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions // <--- Ez a varázslat!
            };

            Console.WriteLine("Csalásfelderítő Agent elemzése folyamatban a Llama 3-on...\n");

            // 5. Meghívjuk az Agentet (elküldjük neki a teljes history-t és a beállításokat)
            var result = await chatCompletionService.GetChatMessageContentAsync(agentChat, executionSettings, kernel);

            // Hozzáadjuk a válaszát a beszélgetéshez (hogy ha tovább kérdezzük, emlékezzen rá)
            agentChat.AddAssistantMessage(result.Content);

            Console.WriteLine($"--- Agent Döntése ---");
            Console.WriteLine(result.Content);


            //Routing
            //----------------------------------------------------


            // 1. Agent válaszának kiíratása
            string fraudAnalysis = result.Content;
            Console.WriteLine($"\n[1. Agent Elemzése]:\n{fraudAnalysis}");

            ChatHistory nextAgentChat;
            string nextAgentName;







            // ================= ROUTING (ÚTVÁLASZTÁS) C#-ban =================

            if (fraudAnalysis.Contains("[CSALÁS]", StringComparison.OrdinalIgnoreCase))
            {
                // HA BAJ VAN: A Biztonsági formázó kapja meg
                nextAgentName = "BIZTONSÁGI JELENTÉS AGENT";
                nextAgentChat = new ChatHistory(
                    "Alakítsd át a kapott szöveget egy strukturált, vázlatpontos IT hibajeggyé (Ticket). " +
                    "Szigorú szabály: Csak a formázott szöveget add vissza! Ne kérdezz vissza, ne magyarázkodj! " +
                    "Használj ilyen pontokat: \n- Tárgy\n- Kockázati szint\n- Érintett IP cím\n- Részletek."
                );
            }
            else
            {
                // HA NINCS BAJ: Ügyfélszolgálatos formázó kapja meg
                nextAgentName = "ÜGYFÉLSZOLGÁLATI AGENT";
                nextAgentChat = new ChatHistory(
                    "Alakítsd át a kapott szöveget egy ügyfélnek szóló, kedves magyar nyelvű levéllé. " +
                    "Írd meg neki, hogy a tranzakcióját megvizsgáltuk és a fiókja biztonságban van. " +
                    "Ne említs IP címeket!"
                );
            }

            Console.WriteLine($"\n[Rendszer] ---> Irányítás a megfelelő ügynökhöz: {nextAgentName}...");
            nextAgentChat.AddUserMessage($"A feldolgozandó nyers elemzés: {fraudAnalysis}");

            // ÚJ ÉS FONTOS RÉSZ: Készítünk egy "Eszköztelen" beállítást a 2. Agentnek!
            // Így a 2. Agent garantáltan nem fog elkezdeni IP-ket meg toolokat lekérdezni, csak szöveget ír.
            var noToolSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                ToolCallBehavior = null // Itt kikapcsoljuk az AutoInvoke-ot!
            };

            // Futtatjuk a 2. Agentet az ESZKÖZTELEN beállításokkal
            var finalResponse = await chatCompletionService.GetChatMessageContentAsync(nextAgentChat, noToolSettings, kernel);

            // Végeredmény kiíratása
            Console.WriteLine($"\n[{nextAgentName} VÁLASZA]:\n{finalResponse.Content}");

            // FONTOS: Ezután a sor után MÁR NE LEGYEN SEMMI a Main metódusban! A maradék "//Válasz agent" blokkokat töröld!
        }









    }

    //plugins
    //----------------------------------------------------------------------------------
    // Ez a mi "Eszköztárunk" (Plugin), amit odaadunk az Agentnek.
    public class IpCheckerPlugin
    {
        [KernelFunction("CheckIpRisk")]
        // PONTOSÍTOTT LEÍRÁS: Elhitetjük a modellel, hogy ez az eszköz a lokációt is megadja, így nem fog hallucinálni egy Geolokációs eszközt!
        [Description("Kizárólag ezzel ellenőrizheted egy bejövő IP cím (pl. 193.45.12.8) kockázatát ÉS Földrajzi Helyzetét. KÖTELEZŐ használni gyanús IP vizsgálatakor!")]
        public string CheckIpRisk([Description("A pontos IP cím, amit a felhasználó megadott")] string ipAddress)
        {
            Console.WriteLine($"\n[C# Tool] ---> AI Agent lefuttatta az IP ellenőrzőt: {ipAddress}");
            
            if (ipAddress.StartsWith("193.")) 
            {
                // Bővített válasz, hogy kielégítse az AI információigényét:
                return "MAGAS KOCKÁZAT: Ez az IP cím riasztást váltott ki. Földrajzi helyzet: Nigéria, Lagos.";
            }
            return "ALACSONY KOCKÁZAT: Az IP cím tiszta.";
        }
    }

    public class NetworkPlugin
    {
        [KernelFunction("GetCurrentPublicIp")]
        // PONTOSÍTOTT LEÍRÁS: Konkrét "if-then" (ha-akkor) logikát adunk a modell szájába.
        [Description("Lekérdezi a felhasználó JELENLEGI, saját IP címét. KÖTELEZŐEN hívd meg ezt is, ha a felhasználó azt kérdezi: 'én voltam-e?', hogy az eredményt össze tudd hasonlítani a gyanús IP-vel!")]
        public async Task<string> GetCurrentPublicIpAsync()
        {
            Console.WriteLine("\n[C# Tool] ---> AI Agent lekérdezi a TE valós IP címedet...");
            
            using var client = new HttpClient();
            try
            {
                string ip = await client.GetStringAsync("https://api.ipify.org");
                Console.WriteLine($"[C# Tool] ---> Lekérdezett jelenlegi IP: {ip}");
                // Ez a mondat segít az LLM-nek megérteni, hogy mit kapott vissza:
                return $"A felhasználó saját, jelenlegi IP címe: {ip}.";
            }
            catch (Exception)
            {
                return "Hiba történt a jelenlegi IP lekérdezésekor.";
            }
        }



    }
}
