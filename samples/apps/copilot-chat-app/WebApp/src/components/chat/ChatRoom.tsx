// Copyright (c) Microsoft. All rights reserved.

import { useAccount } from '@azure/msal-react';
import { makeStyles, shorthands, tokens } from '@fluentui/react-components';
import debug from 'debug';
import React from 'react';
import { Constants } from '../../Constants';
import { useChat } from '../../libs/useChat';
import { useAppDispatch, useAppSelector } from '../../redux/app/hooks';
import { RootState } from '../../redux/app/store';
import { updateConversation } from '../../redux/features/conversations/conversationsSlice';
import { ChatHistory } from './ChatHistory';
import { ChatInput } from './ChatInput';

const log = debug(Constants.debug.root).extend('chat-room');

const useClasses = makeStyles({
    root: {
        height: '100%',
        display: 'grid',
        gridTemplateColumns: '1fr',
        gridTemplateRows: '1fr auto',
        gridTemplateAreas: "'history' 'input'",
    },
    history: {
        ...shorthands.gridArea('history'),
        ...shorthands.padding(tokens.spacingVerticalM),
        overflowY: 'auto',
        display: 'grid',
    },
    input: {
        ...shorthands.gridArea('input'),
        ...shorthands.padding(tokens.spacingVerticalM),
        backgroundColor: tokens.colorNeutralBackground4,
    },
});



export const ChatRoom: React.FC = () => {
    const { audience } = useAppSelector((state: RootState) => state.chat);
    const { conversations, selectedId } = useAppSelector((state: RootState) => state.conversations);
    const messages = conversations[selectedId].messages;
    const classes = useClasses();
    const account = useAccount();
    const dispatch = useAppDispatch();
    const scrollViewTargetRef = React.useRef<HTMLDivElement>(null);
    const scrollTargetRef = React.useRef<HTMLDivElement>(null);
    const [shouldAutoScroll, setShouldAutoScroll] = React.useState(true);
    const chat = useChat();

    React.useEffect(() => {
        if (!shouldAutoScroll) return;
        scrollToTarget(scrollTargetRef.current);
    }, [messages, audience, shouldAutoScroll]);

    React.useEffect(() => {
        const onScroll = () => {
            if (!scrollViewTargetRef.current) return;
            const { scrollTop, scrollHeight, clientHeight } = scrollViewTargetRef.current;
            const isAtBottom = scrollTop + clientHeight >= scrollHeight - 10;
            setShouldAutoScroll(isAtBottom);
        };

        if (!scrollViewTargetRef.current) return;

        const currentScrollViewTarget = scrollViewTargetRef.current;

        currentScrollViewTarget.addEventListener('scroll', onScroll);
        return () => {
            currentScrollViewTarget.removeEventListener('scroll', onScroll);
        };
    }, []);

    if (!account) {
        return null;
    }

    const handleSubmit = async (value: string) => {
        log('submitting user chat message');
        const chatInput = {
                timestamp: new Date().getTime(),
                sender: account?.homeAccountId,
                content: value,
        };
        dispatch(updateConversation({ message: chatInput }));
        await chat.getResponse(value, selectedId);
        setShouldAutoScroll(true);
    };

    return (
        <div className={classes.root}>
            <div ref={scrollViewTargetRef} className={classes.history}>
                <ChatHistory audience={audience} messages={messages} />
                <div>
                    <div ref={scrollTargetRef} />
                </div>
            </div>
            <div className={classes.input}>
                <ChatInput onSubmit={handleSubmit} />
            </div>
        </div>
    );
};

const scrollToTarget = (element: HTMLElement | null) => {
    if (!element) return;
    element.scrollIntoView({ block: 'start', behavior: 'smooth' });
};
