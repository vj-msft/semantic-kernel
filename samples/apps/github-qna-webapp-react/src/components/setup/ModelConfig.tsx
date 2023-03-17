// Copyright (c) Microsoft. All rights reserved.

import { Dropdown, Label, Option, Spinner } from '@fluentui/react-components';
// import { Configuration, OpenAIApi } from 'openai';
import { FC, useEffect, useState } from 'react';
import { IBackendConfig } from '../../model/KeyConfig';

interface IData {
    isOpenAI: boolean;
    modelType: ModelType;
    backendConfig: IBackendConfig;
    setBackendConfig: React.Dispatch<React.SetStateAction<IBackendConfig>>;
    setModel: (value: React.SetStateAction<string>) => void;
    defaultModel?: string;
}

export enum ModelType {
    Embeddings,
    Completion,
}

interface ModelOption {
    id: string;
    disabled: boolean;
}

const ModelConfig: FC<IData> = ({
    isOpenAI,
    modelType,
    setModel,
    backendConfig,
    setBackendConfig,
    defaultModel = '',
}) => {
    const modelTitle = modelType === ModelType.Embeddings ? ['embedding', 'Embedding'] : ['completion', 'Completion'];
    const labelPrefix = `${isOpenAI ? 'oai' : 'aoai'}${modelTitle[0]}`;
    const [modelIds, setModelIds] = useState<ModelOption[] | undefined>();
    const [isBusy, setIsBusy] = useState(false);
    const [selectedModel, setSelectedModel] = useState(defaultModel);

    useEffect(() => {
        setSelectedModel(defaultModel);
        if (
            backendConfig &&
            ((backendConfig?.backend === 1 && isOpenAI) || (backendConfig?.backend === 0 && !isOpenAI))
        ) {
            const getModels = async (isOpenAI: boolean, apiKey: string, aoaiEndpoint?: string) => {
                setModelIds(undefined);
                const currentAoaiApiVersion = '2022-12-01';

                const baseUrl = isOpenAI ? 'https://api.openai.com/v1/' : aoaiEndpoint;
                const path = !isOpenAI ? `/openai/deployments?api-version=${currentAoaiApiVersion}` : 'models';
                const requestUrl = baseUrl + path;

                let init: RequestInit = {
                    method: 'GET',
                    headers: isOpenAI
                        ? { Authorization: `Bearer ${apiKey}` }
                        : {
                              'api-key': apiKey,
                          },
                };

                const onFailure = (errorMessage?: string) => {
                    alert(errorMessage);
                    setIsBusy(false);
                    setSelectedModel('');
                    return undefined;
                };

                let response: Response | undefined = undefined;
                try {
                    response = await fetch(requestUrl, init);
                } catch (e) {
                    return onFailure(e as string);
                }
                if (!response || !response.ok) {
                    return onFailure(await response?.clone().text());
                }

                const models = await response!
                    .clone()
                    .json()
                    .then((body) => {
                        return body.data;
                    });

                const ids: ModelOption[] = [];
                for (const key in models) {
                    const model = models[key];
                    ids.push({ id: model.id, disabled: model.status && model.status !== 'succeeded' });
                }
                return ids;
            };

            const fetchModels = backendConfig.key && ((!isOpenAI && backendConfig.endpoint) || isOpenAI);

            if (fetchModels) {
                setIsBusy(true);
                getModels(isOpenAI, backendConfig.key, isOpenAI ? undefined : backendConfig.endpoint).then((value) => {
                    setModelIds(value);
                    setIsBusy(false);
                });
            }
        }
    }, [backendConfig.key, backendConfig.endpoint]);

    return (
        <div style={{ paddingTop: 20, gap: 10, display: 'flex', flexDirection: 'column', alignItems: 'left' }}>
            <Label htmlFor={`${labelPrefix}model`}>{modelTitle[1]} Model</Label>
            <div style={{ display: 'flex', gap: 10, flexDirection: 'row', alignItems: 'left' }}>
                {isBusy ? <Spinner size="tiny" /> : null}
                <Dropdown
                    aria-labelledby={`${labelPrefix}model`}
                    value={selectedModel}
                    placeholder={
                        modelIds
                            ? 'Select a model id'
                            : `Enter valid key ${isOpenAI ? '' : 'and endpoint to load'} models`
                    }
                    onOptionSelect={(_e, model) => {
                        setSelectedModel(model.optionValue ?? '');
                        setModel(model.optionValue ?? '');
                        setBackendConfig({
                            ...backendConfig,
                            deploymentOrModelId: model.optionValue ?? '',
                            label: model.optionValue ?? '',
                        });
                    }}
                >
                    {modelIds?.map((option) => (
                        <Option key={option.id} disabled={option.disabled}>
                            {option.id}
                        </Option>
                    ))}
                </Dropdown>
            </div>
        </div>
    );
};

export default ModelConfig;