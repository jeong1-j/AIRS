using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class Signal
{
    public readonly string Kind, Label, Reason, Step, Suggestion, Evidence;

    public Signal(string kind, string label, string reason, string step, string suggestion, string evidence)
    {
        Kind = kind;
        Label = label;
        Reason = reason;
        Step = step;
        Suggestion = suggestion;
        Evidence = evidence;
    }
}

internal static class Detector
{
    private const string Ask = @"(?:줘(?:요)?|주세요|주라|줄래(?:요)?|주면안돼(?:요)?|줄수있(?:어(?:요)?|나요|을까(?:요)?)|주시(?:겠어요|겠습니까|면좋겠어요)|주실수있나요|주십시오|부탁(?:해(?:요)?|합니다)|해줄수있어(?:요)?)";
    private const string Product = @"(?:과제코드|이코드|이문제들?|문제들?|과제|숙제|수행평가|(?:탐구|실험)?보고서(?:의?결론)?|실험결론|발표대본|발표자료|에세이|독후감|방정식|이식|이거)";
    private const string Produce = @"(?:완성해|작성해|만들어|써|적어|풀어|해결해|해|짜|구현해)";
    private const string Verify = @"(?:검산|검증|검토|확인|대조|비교|피드백|점검|오류|틀린|맞는지|고칠|부족|반례)";
    private const string Request = @"(?:알려|말해|보여|설명해|찾아|짚어|정리해|제시해|봐|도와|해|내|써)" + Ask;
    public const string Clauses = @"(?:[!?。！？;\n\r]+|(?<!\d)\.|\.(?!\d)|[,，]+|(?<=\s)(?:그리고|하지만|다만|그런데|대신에|그래도)\s+)";
    public const string Quotes = "[“‘「『][^”’」』]*[”’」』]|\"[^\"\\r\\n]*\"|'[^'\\r\\n]+'";
    public const string Mention = @"(?:라고|라는)(?:질문|요청|표현|문장|말|예시)|(?:위험한|부적절한)(?:이유|표현)|(?:하면|믿으면|맡기면|제출하면)안되는이유|(?:달라고|해달라고|써달라고).{0,12}(?:하지마|하지말|하면안)";
    public const string QuoteCommand = @"^\s*(?:이대로|그대로|그대로\s*해\s*줘|그렇게\s*해\s*줘|이\s*요청을\s*실행해\s*줘)[.!?]?\s*$";
    public const string PriorWork = @"(?:내가|제가|직접|스스로|우리가).{0,26}(?:해봤|해보았|풀었|풀어봤|썼|작성했|계산했|구했|생각했|확인했|관찰했|찾았|측정했|해석했|비교했|그렸|실행했)|(?:내|나의|제가쓴|내가푼)(?:풀이|초안|결론|가설|계산|코드|해석|시도|답)|(?:까지)(?:풀었|해봤|구했|썼)|(?:라고|으로)(?:나왔|계산했|측정했|판단했|생각했)";
    public const string Limited = @"(?:힌트|실마리|첫단계|첫줄|방향|어디서부터|틀린단계|오류|반례|이부분|이단계|한단계|다음단계|어느부분|개요|구조|예시|샘플|방법|원리|개념|출처|근거|점검|검토|검산|검증|대조|피드백|비교)";
    public const string Constructive = @"(?:(?:힌트|실마리|첫단계|첫줄|다음단계|방향|어디서부터).{0,18}" + Request
        + @"|(?:내|나의|제가|내가|직접|여기까지|이풀이|이해석).{0,72}" + Verify
        + @"|(?:출처|원자료|실험데이터|측정값|근거|반례).{0,24}(?:대조|찾아|확인|검증|비교|알려|짚어|보여)|(?:검산|검증|확인)(?:할|하는)방법)";
    public const string GoodNegation = @"(?:힌트|검산|검증|검토|출처|근거)(?:은|는|을|를|도)?(?:필요없|하지마|말고|빼고|없이)|(?:정답|답)만(?:원해|달라고했|달라는거)";
    public const string Completion = @"(?:줘|주세요|줄래|알려|설명|부탁|어떻게|인가요|맞나요|볼까|확인할|하는방법|검토|검산|비교|대조|찾아|짚어|보여|돼\?|되나요)";
    public const string ClauseNegation = @"(?:해|써|작성해|풀어|알려|구해|만들어|말해)(?:달라는(?:게|것이)아니|주지(?:는)?마)|(?:답|정답)만(?:원하는(?:게|것이)아니|달라는(?:게|것이)아니)|할필요(?:가)?없(?:지않|(?:는|다는)(?:건|것은|게|것이)아니)|(?:대신(?:써|해)달라는(?:게|건)아니)";
    public const string ContextBridge = @"(?:그|이)(?:대신|부분|단계|방법)|(?:다음|남은|이어서|여기서|어디서|어떤점|검산|검토|검증|대조|확인|피드백)";
    public const string AnswerContext = @"(?:내(?:가|답|풀이|계산|결과)|제가|직접).{0,35}(?:비교|검산|대조|채점|맞는지|틀렸는지|맞춰)|(?:비교|검산|대조|채점)(?:하려고|할게|해볼|하기위해|용으로)";
    private const string BypassCheck = @"(?:비교|검산|대조|채점|검증|확인)(?:은|는|을|를|도)?(?:안하|안할|하지마|하지않|없이|말고|할필요없|필요없|안해도|생략)";
    public const string Partial = @"(?:한|첫|다음|이|그)(?:단계|줄|문장)(?:만|를|을)?|(?:막힌|틀린|이|해당|한)(?:부분|곳|줄)(?:만|을|부터)?|(?:힌트|개요|뼈대|방향|예시|피드백)(?:만|를|을)?";
    public const string ExplicitWhole = @"(?:처음부터끝까지|통째로|전부|그대로제출|제출용|아무것도안하고|전체를?대신|답만)";
    private const string LimitedRequest = @"(?:힌트|개요|뼈대|초안|예시|뭘먼저|어디서부터|첫단계|틀린부분|오류|방법|원리|개념)(?:만|부터|를|을)?(?:어떻게)?(?:알려|말해|보여|설명해|찾아|짚어|정리해|제시해|만들어|작성해|써)" + Ask;

    public const string Skip = @"(?:(?:확인|검증|검산|출처확인|근거확인)(?:은|는|을|를|도)?(?:할필요(?:가)?없|하지(?:는)?마|안해도(?:돼|되|됨)|생략해)|(?:검산|검증|확인|출처|근거)(?:도)?없이(?:그대로)?(?:써|낼|제출|복사|적어|답해|알려)|(?:네|너의|ai|인공지능의?)?답(?:변)?(?:을|은|는)?그대로(?:제출할|써낼|쓸게|복붙할))";
    public const string Answer = @"(?:(?:최종)?(?:정답|답|결과|결론|숫자|[1-9]번답)만(?:바로|그냥|빨리|좀)?(?:" + Request + @"|(?:구해)?" + Ask + @"|내놔|$)|(?:풀이|설명|이유|과정)(?:은|는|을|를)?(?:빼고|없이|생략하고).{0,9}(?:정답|답|결과)(?:만|을)?" + Request + @")";
    public const string HandOff = @"(?:(?:처음부터끝까지|대신|그대로제출할수있게|제출용으로|통째로|전부|다알아서|전부알아서).{0,23}" + Produce + Ask
        + @"|" + Product + @"(?:를|을|는|은|의|도)?(?:네가|너가|니가|그냥|좀|한번|한꺼번에|전부|모두|알아서|다|대신|바로){0,4}" + Produce + Ask
        + @"|(?:그대로|복붙해서|복사해서)(?:바로)?(?:낼|제출할|쓸)(?:수있게|수있도록).{0,26}" + Produce + Ask
        + @"|(?:판단|결정|선택)(?:을|은|는)?(?:전부|다)?(?:네가|너가|알아서)(?:해" + Ask + @"|해$)|(?:답안|완성본)(?:을|을그대로|을대신)?(?:만들어|써|작성해)" + Ask + @")";

    public static readonly string[][] Topics = {
        new[] { "science", @"그래프|실험|측정값|가설|탐구|변인|원자료|실험값",
            "그래프나 실험 기록에서 가장 크게 달라진 구간 하나를 찾아, 수치와 단위를 적어 볼래요?",
            "내가 고른 그래프 구간의 해석이 측정값과 일치하는지 확인해 줘. 결론을 쓰기 전에 살펴볼 다른 변인도 하나 짚어 줘." },
        new[] { "code", @"코드|프로그래밍|파이썬|자바스크립트|함수|디버깅|알고리즘|오류메시지",
            "기대한 출력과 실제 출력이 다른 입력 하나를 골라 볼래요?",
            "내 코드에서 기대한 출력과 실제 출력이 달라지는 지점을 짚어 줘. 내가 고쳐 볼 수 있게 첫 단계 힌트만 알려 줘." },
        new[] { "math", @"수학|방정식|이차|미분|적분|삼각|도형|확률|풀이|정답|검산|등식|문제",
            "주어진 조건 하나를 식으로 옮기고, 어디에서 막히는지 확인해 볼래요?",
            "내 풀이에서 처음 틀린 단계가 있는지 검산해 줘. 막힌 부분은 정답 없이 다음 단계의 힌트만 알려 줘." },
        new[] { "writing", @"보고서|발표|에세이|독후감|글쓰기|대본|초안|감상|문단",
            "내가 말하고 싶은 주장과 그 근거를 한 문장씩 적어 볼래요?",
            "내 초안에서 주장을 뒷받침할 근거가 부족한 문장 하나를 짚어 줘. 내가 직접 고칠 수 있게 방향을 알려 줘." }
    };

    public static readonly string[][] Feedback = {
        new[] { "skip", "검증 생략", "답을 확인하는 단계를 건너뛰려는 요청이에요.",
            "답변에서 확인할 주장 하나를 고르고 어떤 자료로 확인할지 적어 볼래요?",
            "이 답변의 주장 중 근거가 부족한 부분을 짚고, 원자료로 직접 확인할 방법을 알려 줘." },
        new[] { "answer", "답만 요청", "풀이를 점검하는 대신 최종 답만 받으려는 요청이에요.",
            "먼저 주어진 조건 하나를 적고, 첫 줄까지 풀어 볼래요?",
            "정답을 숨긴 채 내가 시도할 첫 단계의 힌트만 알려 줘." },
        new[] { "handoff", "과제 맡기기", "과제의 핵심 결과물을 AI에게 맡기려는 요청이에요.",
            "내가 직접 할 수 있는 첫 단계 하나를 골라 짧게 적어 볼래요?",
            "내 초안에서 근거가 부족한 부분과 직접 고칠 수 있는 부분을 짚어 줘." }
    };

    public static Signal Find(string text)
    {
        var clauses = Split(text);
        var condensed = clauses.Select(Compact).ToArray();
        // Review the most recent request first, using adjacent clauses for intent.
        for (int i = clauses.Length - 1; i >= 0; i--)
        {
            string clause = condensed[i];
            if (Match(clause, Mention) || Match(clause, ClauseNegation)) continue;
            string nearby = string.Concat(condensed.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3));
            if (Match(clause, Skip)) return Coach(0, clause, nearby, clauses[i]);
            bool verifying = Match(nearby, AnswerContext) && !Match(nearby, BypassCheck);
            if (Match(clause, Answer) && !verifying) return Coach(1, clause, nearby, clauses[i]);
            if (Match(clause, HandOff) && !Match(clause, LimitedRequest)
                && (!Match(clause, Partial) || Match(clause, ExplicitWhole)))
                return Coach(2, clause, nearby, clauses[i]);
        }
        return null;
    }

    public static bool IsGood(string text)
    {
        return Find(text) == null && IsConstructive(text);
    }

    public static bool IsConstructive(string text)
    {
        var clauses = Split(text);
        bool hasWork = false;
        foreach (string part in clauses)
        {
            string clause = Compact(part);
            if (Match(clause, Mention) || Match(clause, GoodNegation)) continue;
            hasWork = hasWork || Match(clause, PriorWork);
            if (Match(clause, Constructive) || (hasWork && Match(clause, Limited)
                && Match(clause, ContextBridge) && Match(part, Completion))) return true;
        }
        return false;
    }

    private static string[] Split(string text)
    {
        string clean = (text ?? "").Normalize(NormalizationForm.FormKC);
        bool useQuote = Match(Regex.Replace(clean, Quotes, ""), QuoteCommand);
        if (!useQuote) clean = Regex.Replace(clean, Quotes, " ");
        return Regex.Split(clean, Clauses).Select(part => part.Trim()).Where(part => part.Length > 0).ToArray();
    }

    private static bool Match(string text, string pattern)
    {
        return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }

    private static Signal Coach(int rule, string clause, string context, string evidence)
    {
        var feedback = Feedback[rule];
        var topic = rule == 0 ? null : (Topics.FirstOrDefault(candidate => Match(clause, candidate[1]))
            ?? Topics.FirstOrDefault(candidate => Match(context, candidate[1])));
        string step = topic == null ? feedback[3] : topic[2];
        string suggestion = topic == null ? feedback[4] : topic[3];
        if (rule != 0 && Match(context, PriorWork))
        {
            step = "이미 해본 부분은 살리고, 막힌 지점 한 곳만 골라 도움을 요청해 볼래요?";
        }
        return new Signal(feedback[0], feedback[1], feedback[2], step, suggestion,
            evidence.Length <= 76 ? evidence : evidence.Substring(0, 73) + "…");
    }

    public static string Compact(string text)
    {
        return Regex.Replace(text.Normalize(NormalizationForm.FormKC), @"[\s\p{P}\p{Cf}]+", "").ToLowerInvariant();
    }

    public static string Direction(Signal signal)
    {
        if (signal.Kind == "skip") return "핵심 주장 ↔ 원자료, 하나만 대조해 봐요.";
        var topic = Topics.FirstOrDefault(item => item[3] == signal.Suggestion);
        switch (topic == null ? "" : topic[0])
        {
            case "science": return "그래프에서 변화가 가장 큰 구간부터.";
            case "code": return "예상과 다른 입력 하나부터.";
            case "math": return "주어진 조건 하나를 식으로.";
            case "writing": return "핵심 주장과 근거를 한 줄씩.";
            default: return signal.Kind == "answer" ? "풀이의 첫 줄부터." : "직접 해볼 첫 단계 하나부터.";
        }
    }
}
