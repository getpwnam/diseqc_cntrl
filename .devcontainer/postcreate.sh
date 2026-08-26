#!/usr/bin/env bash

set -e


echo "Setting up environment..."

#pip install -r requirements.txt

# Persistent Bash history
sudo touch /home/vscode/commandhistory/.bash_history
sudo chown "$(id -u):$(id -g)" \
	/home/vscode/commandhistory \
	/home/vscode/commandhistory/.bash_history
chmod 600 /home/vscode/commandhistory/.bash_history

if ! grep -q "### devcontainer persistent bash history ###" ~/.bashrc; then
cat >> ~/.bashrc <<'EOF'
### devcontainer persistent bash history ###
export HISTFILE=/home/vscode/commandhistory/.bash_history
export HISTSIZE=10000
export HISTFILESIZE=20000
shopt -s histappend
case ";${PROMPT_COMMAND:-};" in
	*";history -a; history -n;"*) ;;
	*) PROMPT_COMMAND="history -a; history -n${PROMPT_COMMAND:+; ${PROMPT_COMMAND}}" ;;
esac
### end devcontainer persistent bash history ###

EOF
fi

# Git prompt
if ! grep -q "### devcontainer git prompt ###" ~/.bashrc; then
cat >> ~/.bashrc <<'EOF'
### devcontainer git prompt ###
parse_git_branch() { git branch --show-current 2>/dev/null; }
parse_git_dirty() { [[ -n $(git status --porcelain 2>/dev/null) ]] && printf '*'; }
export PS1='\[\e[1;36m\]\u@\h\[\e[0m\]:\[\e[1;33m\]\w\[\e[0;32m\]$(git rev-parse --is-inside-work-tree >/dev/null 2>&1 && printf " (%s%s)" "$(parse_git_branch)" "$(parse_git_dirty)")\[\e[0m\]\$ '
### end devcontainer git prompt ###

alias nuget="dotnet nuget"


EOF
fi

#git config --global safe.directory '*'

echo "Environment setup complete."