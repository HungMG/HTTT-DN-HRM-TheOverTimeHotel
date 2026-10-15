import { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { ChatOpenAI } from '@langchain/openai';
import { initChatModel } from 'langchain/chat_models/universal';

const SUPPORTED_PROVIDERS = [
  'openai',
  'anthropic',
  'azure_openai',
  'cohere',
  'google-vertexai',
  'google-vertexai-web',
  'google-genai',
  'ollama',
  'together',
  'fireworks',
  'mistralai',
  'groq',
  'bedrock',
  'cerebras',
  'deepseek',
  'xai',
] as const;
/**
 * Load a chat model from a fully specified name.
 * @param fullySpecifiedName - String in the format 'provider/model' or 'provider/account/provider/model'.
 * @returns A Promise that resolves to a BaseChatModel instance.
 */
export async function loadChatModel(
  fullySpecifiedName: string,
  temperature: number = 0.2,
): Promise<BaseChatModel> {
  const customApiKey = process.env.AI_API_KEY || process.env.OPENAI_API_KEY;
  const customBaseURL = process.env.AI_BASE_URL || process.env.OPENAI_BASE_URL;
  const customModel = process.env.AI_MODEL;

  // If a custom base URL or custom model is set, use ChatOpenAI targeting that endpoint/model
  if (customBaseURL || (customApiKey && customModel)) {
    const model =
      customModel ||
      (fullySpecifiedName.includes('/')
        ? fullySpecifiedName.split('/')[1]
        : fullySpecifiedName);

    const chatModel = new ChatOpenAI({
      model,
      temperature,
      apiKey: customApiKey,
      maxRetries: 1,
      timeout: 25000,
      configuration: customBaseURL ? { baseURL: customBaseURL } : undefined,
    });

    // Remove parameters not supported by some third-party endpoints like Google Gemini
    const origInvocationParams = chatModel.invocationParams.bind(chatModel);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    chatModel.invocationParams = function (options: any) {
      const params = origInvocationParams(options);
      delete params.frequency_penalty;
      delete params.presence_penalty;
      return params;
    };

    return chatModel;
  }
  const index = fullySpecifiedName.indexOf('/');
  if (index === -1) {
    // If there's no "/", assume it's just the model
    if (
      !SUPPORTED_PROVIDERS.includes(
        fullySpecifiedName as (typeof SUPPORTED_PROVIDERS)[number],
      )
    ) {
      throw new Error(`Unsupported model: ${fullySpecifiedName}`);
    }
    return await initChatModel(fullySpecifiedName, {
      temperature: temperature,
    });
  } else {
    const provider = fullySpecifiedName.slice(0, index);
    const model = fullySpecifiedName.slice(index + 1);
    if (
      !SUPPORTED_PROVIDERS.includes(
        provider as (typeof SUPPORTED_PROVIDERS)[number],
      )
    ) {
      throw new Error(`Unsupported provider: ${provider}`);
    }
    return await initChatModel(model, {
      modelProvider: provider,
      temperature: temperature,
    });
  }
}
